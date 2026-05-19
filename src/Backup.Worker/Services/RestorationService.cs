using Backup.Core.Entities;
using Backup.Core.Interfaces;
using Discord;
using Discord.Webhook;
using Discord.WebSocket;

namespace Backup.Worker.Services;

public class RestorationService
{
    private const int WebhookMessageDelayMs = 2500;
    private const string WebhookName = "BackupRestore";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RestorationService> _logger;

    public RestorationService(
        IServiceScopeFactory scopeFactory,
        ILogger<RestorationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task RestoreLayoutAsync(SocketSlashCommand command, Guid backupId, CancellationToken cancellationToken = default)
    {
        await command.RespondAsync("⏳ Restoring server layout (Roles, Categories, empty Channels)...");
        var dashboardMessage = await command.GetOriginalResponseAsync();

        var guild = (command.Channel as SocketGuildChannel)?.Guild;
        if (guild is null) return;

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IBackupRepository>();

        var backup = await repo.GetBackupAsync(backupId);
        if (backup is null)
        {
            await dashboardMessage.ModifyAsync(m => m.Content = $"❌ No backup found with ID `{backupId}`.");
            return;
        }

        await RestoreRolesAsync(guild, backup, cancellationToken);
        var categoryMap = await RestoreCategoriesAsync(guild, backup, cancellationToken);
        await RestoreTextChannelsAsync(guild, backup, categoryMap, cancellationToken);

        await dashboardMessage.ModifyAsync(m => m.Content = "✅ **Phase 1 Complete:** Server layout restored! You can now use `/restore-channel` to populate individual channels with their message history.");
    }

    public async Task RestoreSingleChannelAsync(
        SocketSlashCommand command,
        Guid backupId,
        string originalChannelName,
        ITextChannel targetChannel,
        CancellationToken cancellationToken = default)
    {
        await command.RespondAsync($"⏳ Starting background restoration for {targetChannel.Mention}... See the dashboard message below.", ephemeral: true);
        var dashboardMessage = await command.Channel.SendMessageAsync($"⏳ Initializing restoration dashboard for {targetChannel.Mention}...");

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IBackupRepository>();

        var backup = await repo.GetBackupAsync(backupId);
        if (backup is null) return;

        var dbChannel = backup.Channels.FirstOrDefault(c => c.Name.Equals(originalChannelName, StringComparison.OrdinalIgnoreCase));
        if (dbChannel is null)
        {
            await dashboardMessage.ModifyAsync(m => m.Content = $"❌ Could not find a channel named `{originalChannelName}` in this backup.");
            return;
        }

        int skip = await repo.GetRestoreProgressAsync(dbChannel.ChannelId);
        var messages = await repo.GetMessagesByChannelAsync(dbChannel.ChannelId, skip);

        if (messages.Count == 0)
        {
            await dashboardMessage.ModifyAsync(m => m.Content = $"📭 `{originalChannelName}` has no messages left to restore (or is empty).");
            return;
        }

        int totalExpected = skip + messages.Count;
        string resumeText = skip > 0 ? $" (Resuming from message {skip:N0})" : "";
        await dashboardMessage.ModifyAsync(m => m.Content = $"🚀 Injecting messages from `{originalChannelName}` into {targetChannel.Mention}...{resumeText}");

        var webhook = await GetOrCreateWebhookAsync(targetChannel);
        using var webhookClient = new DiscordWebhookClient(webhook.Id, webhook.Token!);

        var context = new ChannelRestoreContext(
            Repo: repo,
            DbChannelId: dbChannel.ChannelId,
            TargetChannel: targetChannel,
            DashboardMessage: dashboardMessage,
            WebhookClient: webhookClient,
            TotalExpected: totalExpected
        );

        int finalCount = await ProcessMessagesAsync(messages, context, skip, cancellationToken);

        await repo.UpdateRestoreProgressAsync(dbChannel.ChannelId, finalCount);
        await dashboardMessage.ModifyAsync(m => m.Content = $"✅ **Channel Restored!** Successfully injected {finalCount:N0} total messages into {targetChannel.Mention}.");
    }

    private async Task<int> ProcessMessagesAsync(
        IReadOnlyList<BackupMessage> messages,
        ChannelRestoreContext context,
        int currentCount,
        CancellationToken cancellationToken)
    {
        foreach (var message in messages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await PostSingleMessageAsync(context.WebhookClient, message, context.TargetChannel);
            currentCount++;

            if (currentCount % 5 == 0)
            {
                await context.Repo.UpdateRestoreProgressAsync(context.DbChannelId, currentCount);
            }

            if (currentCount % 15 == 0)
            {
                await UpdateDashboardAsync(context.DashboardMessage, context.TargetChannel, currentCount, context.TotalExpected);
            }

            await Task.Delay(WebhookMessageDelayMs, cancellationToken);
        }

        return currentCount;
    }

    private async Task PostSingleMessageAsync(DiscordWebhookClient webhookClient, BackupMessage message, ITextChannel targetChannel)
    {
        try
        {
            var content = string.IsNullOrWhiteSpace(message.Content) ? "\u200b" : message.Content;
            await webhookClient.SendMessageAsync(
                text: content,
                username: message.Author,
                avatarUrl: string.IsNullOrEmpty(message.AvatarUrl) ? null : message.AvatarUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post message {MessageId} to #{ChannelName}.", message.MessageId, targetChannel.Name);
        }
    }

    private async Task UpdateDashboardAsync(IUserMessage dashboardMessage, ITextChannel targetChannel, int currentCount, int totalExpected)
    {
        try
        {
            double percentage = Math.Round((double)currentCount / totalExpected * 100, 1);
            int filledBlocks = Math.Clamp((int)Math.Round(percentage / 10), 0, 10);
            string progressBar = new string('█', filledBlocks) + new string('░', 10 - filledBlocks);

            int remainingMessages = totalExpected - currentCount;
            TimeSpan eta = TimeSpan.FromMilliseconds(remainingMessages * WebhookMessageDelayMs);

            string timeString = eta.TotalHours >= 1
                ? $"{(int)eta.TotalHours}h {eta.Minutes}m"
                : $"{eta.Minutes}m {eta.Seconds}s";

            string dashboardContent =
                $"🚀 **Injecting messages into {targetChannel.Mention}**\n" +
                $"📊 Progress: `{progressBar}` **{percentage}%**\n" +
                $"🔢 Count: **{currentCount:N0} / {totalExpected:N0}**\n" +
                $"⏳ ETA: **{timeString}**";

            await dashboardMessage.ModifyAsync(m => m.Content = dashboardContent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to update dashboard UI at {Count}/{Total}. Discord API error: {Message}", currentCount, totalExpected, ex.Message);
        }
    }

    private async Task RestoreRolesAsync(SocketGuild guild, ServerBackup backup, CancellationToken ct)
    {
        var roles = backup.Roles.Where(r => r.Name != "@everyone").OrderBy(r => r.Position).ToList();
        foreach (var role in roles)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (!guild.Roles.Any(r => r.Name == role.Name))
                {
                    await guild.CreateRoleAsync(role.Name, new GuildPermissions(role.Permissions), new Color(role.Color), false, false);
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "Failed to create role: {RoleName}", role.Name); }
        }
    }

    private async Task<Dictionary<ulong, ulong>> RestoreCategoriesAsync(SocketGuild guild, ServerBackup backup, CancellationToken ct)
    {
        var map = new Dictionary<ulong, ulong>();
        var categories = backup.Channels.Where(c => c.Type == "Category").OrderBy(c => c.Position).ToList();

        foreach (var category in categories)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var created = await guild.CreateCategoryChannelAsync(category.Name, props => props.Position = category.Position);
                map[category.ChannelId] = created.Id;
            }
            catch (Exception ex) { _logger.LogError(ex, "Failed to create category: {CategoryName}", category.Name); }
        }
        return map;
    }

    private async Task RestoreTextChannelsAsync(SocketGuild guild, ServerBackup backup, Dictionary<ulong, ulong> categoryMap, CancellationToken ct)
    {
        var textChannels = backup.Channels.Where(c => c.Type == "Text" || c.Type == "TextChannel" || c.Type == "News").OrderBy(c => c.Position).ToList();

        foreach (var channel in textChannels)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await guild.CreateTextChannelAsync(channel.Name, props =>
                {
                    props.Position = channel.Position;
                    if (channel.ParentId.HasValue && categoryMap.TryGetValue(channel.ParentId.Value, out var newCategoryId))
                        props.CategoryId = newCategoryId;
                });
            }
            catch (Exception ex) { _logger.LogError(ex, "Failed to create channel: #{ChannelName}", channel.Name); }
        }
    }

    private async Task<IWebhook> GetOrCreateWebhookAsync(ITextChannel channel)
    {
        var webhooks = await channel.GetWebhooksAsync();
        var existing = webhooks.FirstOrDefault(w => w.Name == WebhookName);
        return existing ?? await channel.CreateWebhookAsync(WebhookName);
    }

    private record ChannelRestoreContext(
        IBackupRepository Repo,
        ulong DbChannelId,
        ITextChannel TargetChannel,
        IUserMessage DashboardMessage,
        DiscordWebhookClient WebhookClient,
        int TotalExpected
    );
}