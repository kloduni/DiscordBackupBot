using Backup.Core.Entities;
using Backup.Core.Interfaces;
using Discord;
using Discord.WebSocket;

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

    public async Task ExecuteAsync(SocketSlashCommand command, CancellationToken cancellationToken = default)
    {
        await command.RespondAsync("⏳ Backup started. This may take a while...", ephemeral: true);

        var guild = (command.Channel as SocketGuildChannel)?.Guild;
        if (guild is null)
        {
            await command.ModifyOriginalResponseAsync(m => m.Content = "❌ This command must be used inside a server.");
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

        _logger.LogInformation("Backup {BackupId} started for guild {GuildName} ({GuildId})",
            backup.Id, guild.Name, guild.Id);

        await SaveRolesAsync(repo, backup.Id, guild);
        await SaveChannelsAsync(repo, backup.Id, guild);
        await repo.SaveChangesAsync();

        var textChannels = guild.TextChannels
            .OrderBy(c => c.Position)
            .ToList();

        int channelIndex = 0;
        foreach (var channel in textChannels)
        {
            channelIndex++;
            _logger.LogInformation("Fetching messages from #{ChannelName} ({Index}/{Total})",
                channel.Name, channelIndex, textChannels.Count);

            try
            {
                await FetchAndSaveMessagesAsync(repo, channel, cancellationToken);
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

        _logger.LogInformation("Backup {BackupId} completed for guild {GuildName}", backup.Id, guild.Name);

        await command.ModifyOriginalResponseAsync(m =>
            m.Content = $"✅ Backup complete! ID: `{backup.Id}`");
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

    private async Task SaveChannelsAsync(IBackupRepository repo, Guid backupId, IGuild guild)
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
        });

        await repo.AddChannelsAsync(channels);
    }

    private async Task FetchAndSaveMessagesAsync(
        IBackupRepository repo,
        ITextChannel channel,
        CancellationToken cancellationToken)
    {
        ulong? beforeId = await repo.GetLastMessageIdAsync(channel.Id);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyCollection<IMessage> batch;

            if (beforeId.HasValue)
            {
                batch = (await channel.GetMessagesAsync(beforeId.Value, Direction.Before, 100).FlattenAsync()).ToList();
            }
            else
            {
                batch = (await channel.GetMessagesAsync(100).FlattenAsync()).ToList();
            }

            if (batch.Count == 0)
            {
                _logger.LogInformation("#{ChannelName}: no more messages.", channel.Name);
                break;
            }

            var entities = batch.Select(m => new BackupMessage
            {
                Id = Guid.NewGuid(),
                ChannelId = channel.Id,
                MessageId = m.Id,
                Author = m.Author is null ? "Unknown" : $"{m.Author.Username}#{m.Author.Discriminator}",
                AvatarUrl = m.Author?.GetAvatarUrl() ?? m.Author?.GetDefaultAvatarUrl() ?? string.Empty,
                Content = m.Content ?? string.Empty,
                Timestamp = m.Timestamp
            }).ToList();

            await repo.AddMessagesAsync(entities);

            beforeId = batch.Min(m => m.Id);

            _logger.LogInformation("#{ChannelName}: saved {Count} messages. Oldest ID: {OldestId}",
                channel.Name, batch.Count, beforeId);

            await Task.Delay(2500, cancellationToken);

            if (batch.Count < 100)
            {
                _logger.LogInformation("#{ChannelName}: reached end of history.", channel.Name);
                break;
            }
        }
    }
}
