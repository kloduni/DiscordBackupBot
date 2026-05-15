using Backup.Core.Entities;
using Backup.Core.Interfaces;
using Discord;
using Discord.Webhook;
using Discord.WebSocket;

namespace Backup.Worker.Services;

public class RestorationService
{
    private const int WebhookMessageDelayMs = 1500;
    private const string WebhookName = "BackupRestore";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DiscordSocketClient _client;
    private readonly ILogger<RestorationService> _logger;

    public RestorationService(
        IServiceScopeFactory scopeFactory,
        DiscordSocketClient client,
        ILogger<RestorationService> logger)
    {
        _scopeFactory = scopeFactory;
        _client = client;
        _logger = logger;
    }

    public async Task ExecuteAsync(SocketSlashCommand command, Guid backupId, CancellationToken cancellationToken = default)
    {
        await command.RespondAsync("⏳ Restoration started. This may take a long time...", ephemeral: true);

        var guild = (command.Channel as SocketGuildChannel)?.Guild;
        if (guild is null)
        {
            await command.ModifyOriginalResponseAsync(m => m.Content = "❌ This command must be used inside a server.");
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IBackupRepository>();

        var backup = await repo.GetBackupAsync(backupId);
        if (backup is null)
        {
            await command.ModifyOriginalResponseAsync(m =>
                m.Content = $"❌ No backup found with ID `{backupId}`.");
            return;
        }

        _logger.LogInformation("Restoration of backup {BackupId} started on guild {GuildName}",
            backupId, guild.Name);

        await RestoreRolesAsync(guild, backup, cancellationToken);

        var categoryMap = await RestoreCategoriesAsync(guild, backup, cancellationToken);

        var channelMap = await RestoreTextChannelsAsync(guild, backup, categoryMap, cancellationToken);

        await RestoreMessagesAsync(repo, channelMap, cancellationToken);

        _logger.LogInformation("Restoration of backup {BackupId} completed on guild {GuildName}",
            backupId, guild.Name);

        await command.ModifyOriginalResponseAsync(m => m.Content = "✅ Restoration complete!");
    }

    private async Task RestoreRolesAsync(SocketGuild guild, ServerBackup backup, CancellationToken ct)
    {
        var roles = backup.Roles
            .Where(r => r.Name != "@everyone")
            .OrderBy(r => r.Position)
            .ToList();

        foreach (var role in roles)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await guild.CreateRoleAsync(
                    name: role.Name,
                    permissions: new GuildPermissions(role.Permissions),
                    color: new Color(role.Color),
                    isHoisted: false,
                    isMentionable: false);

                _logger.LogInformation("Created role: {RoleName}", role.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create role: {RoleName}", role.Name);
            }
        }
    }

    private async Task<Dictionary<ulong, ulong>> RestoreCategoriesAsync(
        SocketGuild guild,
        ServerBackup backup,
        CancellationToken ct)
    {
        var oldToNewIdMap = new Dictionary<ulong, ulong>();

        var categories = backup.Channels
            .Where(c => c.Type == "Category")
            .OrderBy(c => c.Position)
            .ToList();

        foreach (var category in categories)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var created = await guild.CreateCategoryChannelAsync(category.Name, props =>
                    props.Position = category.Position);

                oldToNewIdMap[category.ChannelId] = created.Id;
                _logger.LogInformation("Created category: {CategoryName}", category.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create category: {CategoryName}", category.Name);
            }
        }

        return oldToNewIdMap;
    }

    private async Task<Dictionary<ulong, ITextChannel>> RestoreTextChannelsAsync(
        SocketGuild guild,
        ServerBackup backup,
        Dictionary<ulong, ulong> categoryMap,
        CancellationToken ct)
    {
        var channelMap = new Dictionary<ulong, ITextChannel>();

        var textChannels = backup.Channels
            .Where(c => c.Type == "Text" || c.Type == "TextChannel" || c.Type == "News")
            .OrderBy(c => c.Position)
            .ToList();

        foreach (var channel in textChannels)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var created = await guild.CreateTextChannelAsync(channel.Name, props =>
                {
                    props.Position = channel.Position;

                    if (channel.ParentId.HasValue && categoryMap.TryGetValue(channel.ParentId.Value, out var newCategoryId))
                        props.CategoryId = newCategoryId;
                });

                channelMap[channel.ChannelId] = created;
                _logger.LogInformation("Created channel: #{ChannelName}", channel.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create channel: #{ChannelName}", channel.Name);
            }
        }

        return channelMap;
    }

    private async Task RestoreMessagesAsync(
        IBackupRepository repo,
        Dictionary<ulong, ITextChannel> channelMap,
        CancellationToken ct)
    {
        foreach (var (oldChannelId, newChannel) in channelMap)
        {
            ct.ThrowIfCancellationRequested();

            var messages = await repo.GetMessagesByChannelAsync(oldChannelId);
            if (messages.Count == 0)
            {
                _logger.LogInformation("#{ChannelName}: no messages to restore. Skipping.", newChannel.Name);
                continue;
            }

            _logger.LogInformation("Restoring {Count} messages into #{ChannelName}",
                messages.Count, newChannel.Name);

            var webhook = await GetOrCreateWebhookAsync(newChannel);

            using var webhookClient = new DiscordWebhookClient(webhook.Id, webhook.Token!);

            foreach (var message in messages)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var content = string.IsNullOrWhiteSpace(message.Content)
                        ? "\u200b"
                        : message.Content;

                    await webhookClient.SendMessageAsync(
                        text: content,
                        username: message.Author,
                        avatarUrl: string.IsNullOrEmpty(message.AvatarUrl) ? null : message.AvatarUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to post message {MessageId} to #{ChannelName}.",
                        message.MessageId, newChannel.Name);
                }

                await Task.Delay(WebhookMessageDelayMs, ct);
            }

            _logger.LogInformation("#{ChannelName}: message restoration complete.", newChannel.Name);
        }
    }

    private async Task<IWebhook> GetOrCreateWebhookAsync(ITextChannel channel)
    {
        var webhooks = await channel.GetWebhooksAsync();
        var existing = webhooks.FirstOrDefault(w => w.Name == WebhookName);

        if (existing is not null)
            return existing;

        return await channel.CreateWebhookAsync(WebhookName);
    }
}
