using Backup.Core.Entities;
using Backup.Core.Interfaces;
using Discord;
using Discord.WebSocket;
using System.Diagnostics;

namespace Backup.Worker.Services;

public class BackupExtractionService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackupExtractionService> _logger;

    public BackupExtractionService(
        IServiceScopeFactory scopeFactory,
        ILogger<BackupExtractionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    private class BackupState
    {
        public Guid BackupId { get; set; }
        public string GuildName { get; set; } = string.Empty;
        public int TotalChannels { get; set; }
        public int CurrentChannelIndex { get; set; }
        public string CurrentChannelName { get; set; } = string.Empty;
        public int TotalMessagesSaved { get; set; }
        public Stopwatch Stopwatch { get; set; } = new();
        public DateTime LastEmbedUpdate { get; set; } = DateTime.MinValue;
    }

    public async Task ExecuteAsync(SocketSlashCommand command, CancellationToken cancellationToken = default)
    {
        await command.RespondAsync("Initializing backup dashboard...");

        var dashboardMessage = await command.GetOriginalResponseAsync();

        var guild = (command.Channel as SocketGuildChannel)?.Guild;
        if (guild is null)
        {
            await dashboardMessage.ModifyAsync(m => m.Content = "❌ This command must be used inside a server.");
            return;
        }

        await guild.DownloadUsersAsync();

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IBackupRepository>();

        var backup = await repo.CreateBackupAsync(new ServerBackup
        {
            GuildId = guild.Id,
            GuildName = guild.Name
        });

        var textChannels = guild.TextChannels.OrderBy(c => c.Position).ToList();

        var state = new BackupState
        {
            BackupId = backup.Id,
            GuildName = guild.Name,
            TotalChannels = textChannels.Count,
            Stopwatch = Stopwatch.StartNew()
        };

        await SaveRolesAsync(repo, backup.Id, guild);
        var dbChannels = await SaveChannelsAsync(repo, backup.Id, guild);
        await repo.SaveChangesAsync();

        foreach (var channel in textChannels)
        {
            state.CurrentChannelIndex++;
            state.CurrentChannelName = channel.Name;

            await UpdateProgressEmbedAsync(dashboardMessage, state, false);

            try
            {
                var dbChannelId = dbChannels.First(c => c.ChannelId == channel.Id).Id;
                await FetchAndSaveMessagesAsync(repo, channel, dbChannelId, dashboardMessage, state, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch messages from #{ChannelName}. Skipping.", channel.Name);
            }
        }

        state.Stopwatch.Stop();
        await UpdateProgressEmbedAsync(dashboardMessage, state, true);
    }

    public async Task ResumeAsync(SocketSlashCommand command, Guid backupId, CancellationToken cancellationToken = default)
    {
        await command.RespondAsync($"Looking up backup `{backupId}`...");

        var dashboardMessage = await command.GetOriginalResponseAsync();

        var guild = (command.Channel as SocketGuildChannel)?.Guild;
        if (guild is null) return;

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IBackupRepository>();

        var backup = await repo.GetBackupAsync(backupId);
        if (backup is null || backup.GuildId != guild.Id)
        {
            await dashboardMessage.ModifyAsync(m => m.Content = $"❌ Backup `{backupId}` not found for this server.");
            return;
        }

        var textChannels = guild.TextChannels.OrderBy(c => c.Position).ToList();
        var dbChannels = backup.Channels.ToList();

        int previousMessages = 0;
        foreach (var dbChannel in dbChannels)
        {
            previousMessages += (await repo.GetMessagesByChannelAsync(dbChannel.ChannelId)).Count;
        }

        var state = new BackupState
        {
            BackupId = backup.Id,
            GuildName = guild.Name,
            TotalChannels = textChannels.Count,
            TotalMessagesSaved = previousMessages,
            Stopwatch = Stopwatch.StartNew()
        };

        foreach (var channel in textChannels)
        {
            state.CurrentChannelIndex++;
            state.CurrentChannelName = channel.Name;

            await UpdateProgressEmbedAsync(dashboardMessage, state, false);

            try
            {
                var dbChannel = dbChannels.FirstOrDefault(c => c.ChannelId == channel.Id);
                if (dbChannel is null) continue;

                await FetchAndSaveMessagesAsync(repo, channel, dbChannel.Id, dashboardMessage, state, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch messages from #{ChannelName}. Skipping.", channel.Name);
            }
        }

        state.Stopwatch.Stop();
        await UpdateProgressEmbedAsync(dashboardMessage, state, true);
    }

    private async Task FetchAndSaveMessagesAsync(
        IBackupRepository repo,
        ITextChannel channel,
        Guid dbChannelId,
        IUserMessage dashboardMessage,
        BackupState state,
        CancellationToken cancellationToken)
    {
        ulong? beforeId = await repo.GetLastMessageIdAsync(channel.Id);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batch = beforeId.HasValue
                ? (await channel.GetMessagesAsync(beforeId.Value, Direction.Before, 100).FlattenAsync()).ToList()
                : (await channel.GetMessagesAsync(100).FlattenAsync()).ToList();

            if (batch.Count == 0) break;

            var entities = batch.Select(m => new BackupMessage
            {
                Id = Guid.NewGuid(),
                BackupChannelId = dbChannelId,
                ChannelId = channel.Id,
                MessageId = m.Id,
                Author = m.Author is null ? "Unknown" : $"{m.Author.Username}#{m.Author.Discriminator}",
                AvatarUrl = m.Author?.GetAvatarUrl() ?? m.Author?.GetDefaultAvatarUrl() ?? string.Empty,
                Content = m.Content ?? string.Empty,
                Timestamp = m.Timestamp
            }).ToList();

            await repo.AddMessagesAsync(entities);

            state.TotalMessagesSaved += batch.Count;
            beforeId = batch.Min(m => m.Id);

            if ((DateTime.UtcNow - state.LastEmbedUpdate).TotalSeconds >= 7)
            {
                await UpdateProgressEmbedAsync(dashboardMessage, state, false);
                state.LastEmbedUpdate = DateTime.UtcNow;
            }

            await Task.Delay(2500, cancellationToken);

            if (batch.Count < 100) break;
        }
    }

    private async Task UpdateProgressEmbedAsync(IUserMessage message, BackupState state, bool isComplete)
    {
        var percent = state.TotalChannels == 0 ? 0 : (int)Math.Round((double)state.CurrentChannelIndex / state.TotalChannels * 100);

        var elapsedSeconds = state.Stopwatch.Elapsed.TotalSeconds;
        var messagesPerSecond = elapsedSeconds > 0 ? (int)(state.TotalMessagesSaved / elapsedSeconds) : 0;

        var embed = new EmbedBuilder()
            .WithTitle(isComplete ? "✅ Backup Complete" : "🛡️ Backup In Progress")
            .WithColor(isComplete ? Color.Green : Color.Blue)
            .WithDescription($"**Target:** {state.GuildName}\n**ID:** `{state.BackupId}`")
            .AddField("📁 Channels Processed", $"{state.CurrentChannelIndex} / {state.TotalChannels} ({percent}%)", inline: true)
            .AddField("💬 Messages Saved", $"{state.TotalMessagesSaved:N0}", inline: true)
            .AddField("🚀 Average Speed", $"{messagesPerSecond:N0} msgs/sec", inline: true)
            .AddField("🔄 Current Task", isComplete ? "All data secured." : $"Fetching `#{state.CurrentChannelName}`...", inline: false)

            .WithFooter(isComplete
                ? $"Total Time Taken: {state.Stopwatch.Elapsed:hh\\:mm\\:ss}"
                : $"Elapsed Time: {state.Stopwatch.Elapsed:hh\\:mm\\:ss}")
            .WithCurrentTimestamp();

        try
        {
            await message.ModifyAsync(m =>
            {
                m.Content = "";
                m.Embed = embed.Build();
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update Discord progress embed (likely rate limited).");
        }
    }

    private async Task SaveRolesAsync(IBackupRepository repo, Guid backupId, IGuild guild)
    {
        var roles = guild.Roles.Cast<SocketRole>().Select(role => new BackupRole
        {
            Id = Guid.NewGuid(),
            ServerBackupId = backupId,
            RoleId = role.Id,
            Name = role.Name,
#pragma warning disable CS0618
            Color = (uint)role.Color.RawValue,
#pragma warning restore CS0618
            Position = role.Position,
            Permissions = role.Permissions.RawValue
        });
        await repo.AddRolesAsync(roles);
    }

    private async Task<List<BackupChannel>> SaveChannelsAsync(IBackupRepository repo, Guid backupId, IGuild guild)
    {
        var socketGuild = (SocketGuild)guild;
        var channels = socketGuild.Channels.Select(channel => new BackupChannel
        {
            Id = Guid.NewGuid(),
            ServerBackupId = backupId,
            ChannelId = channel.Id,
            Name = channel.Name,
            Type = channel.GetChannelType()?.ToString() ?? "Unknown",
            Position = channel is INestedChannel nested ? nested.Position : 0,
            ParentId = channel is INestedChannel nc ? nc.CategoryId : null
        }).ToList();

        await repo.AddChannelsAsync(channels);
        return channels;
    }
}