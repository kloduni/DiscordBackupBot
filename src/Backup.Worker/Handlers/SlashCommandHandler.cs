using Backup.Core.Interfaces;
using Backup.Worker.Services;
using Discord;
using Discord.WebSocket;

namespace Backup.Worker.Handlers;

public class SlashCommandHandler
{
    private readonly BackupExtractionService _extractionService;
    private readonly RestorationService _restorationService;
    private readonly DiscordSocketClient _client;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SlashCommandHandler> _logger;

    public SlashCommandHandler(
        BackupExtractionService extractionService,
        RestorationService restorationService,
        DiscordSocketClient client,
        IServiceScopeFactory scopeFactory,
        ILogger<SlashCommandHandler> logger)
    {
        _extractionService = extractionService;
        _restorationService = restorationService;
        _client = client;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void Register()
    {
        _client.SlashCommandExecuted += OnSlashCommandExecutedAsync;
    }

    private Task OnSlashCommandExecutedAsync(SocketSlashCommand command)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                switch (command.CommandName)
                {
                    case "backup-create":
                        await _extractionService.ExecuteAsync(command);
                        break;

                    case "backup-list":
                        await HandleListAsync(command);
                        break;

                    case "restore-layout":
                        await HandleRestoreLayoutAsync(command);
                        break;

                    case "restore-channel":
                        await HandleRestoreChannelAsync(command);
                        break;

                    case "backup-resume":
                        await HandleResumeAsync(command);
                        break;

                    default:
                        await command.RespondAsync($"Unknown command: `{command.CommandName}`", ephemeral: true);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in slash command '{CommandName}'.", command.CommandName);
            }
        });

        return Task.CompletedTask;
    }

    private async Task HandleListAsync(SocketSlashCommand command)
    {
        await command.DeferAsync(ephemeral: true);

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IBackupRepository>();

        var backups = await repo.GetAllBackupsAsync();

        if (backups.Count == 0)
        {
            await command.FollowupAsync("📭 No backups found in the database.", ephemeral: true);
            return;
        }

        var lines = backups.Select(b =>
            $"• `{b.Id}` — **{b.GuildName}** ({b.CreatedAt:yyyy-MM-dd})");

        var message = $"📋 **All Saved Server Backups ({backups.Count}):**\n" + string.Join("\n", lines);

        if (message.Length > 2000)
        {
            message = message.Substring(0, 1995) + "...";
        }

        await command.FollowupAsync(message, ephemeral: true);
    }

    private async Task HandleRestoreLayoutAsync(SocketSlashCommand command)
    {
        var idOption = command.Data.Options.FirstOrDefault(o => o.Name == "id");
        if (idOption?.Value is not string idString || !Guid.TryParse(idString, out var backupId))
        {
            await command.RespondAsync("❌ Invalid or missing backup ID.", ephemeral: true);
            return;
        }

        await _restorationService.RestoreLayoutAsync(command, backupId);
    }

    private async Task HandleRestoreChannelAsync(SocketSlashCommand command)
    {
        var idOption = command.Data.Options.FirstOrDefault(o => o.Name == "id");
        var nameOption = command.Data.Options.FirstOrDefault(o => o.Name == "original-name");
        var targetOption = command.Data.Options.FirstOrDefault(o => o.Name == "target");

        if (idOption?.Value is not string idString || !Guid.TryParse(idString, out var backupId) ||
            nameOption?.Value is not string originalName ||
            targetOption?.Value is not ITextChannel targetChannel)
        {
            await command.RespondAsync("❌ Invalid parameters provided.", ephemeral: true);
            return;
        }

        await _restorationService.RestoreSingleChannelAsync(command, backupId, originalName, targetChannel);
    }

    private async Task HandleResumeAsync(SocketSlashCommand command)
    {
        var idOption = command.Data.Options.FirstOrDefault(o => o.Name == "id");
        if (idOption?.Value is not string idString || !Guid.TryParse(idString, out var backupId))
        {
            await command.RespondAsync("❌ Invalid or missing backup ID.", ephemeral: true);
            return;
        }

        await _extractionService.ResumeAsync(command, backupId);
    }
}