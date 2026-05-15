using Backup.Core.Interfaces;
using Backup.Worker.Services;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

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

                    case "backup-restore":
                        await HandleRestoreAsync(command);
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
        if (command.GuildId is not ulong guildId)
        {
            await command.RespondAsync("❌ This command must be used inside a server.", ephemeral: true);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IBackupRepository>();

        var backups = await repo.GetBackupsByGuildAsync(guildId);

        if (backups.Count == 0)
        {
            await command.RespondAsync("📭 No backups found for this server.", ephemeral: true);
            return;
        }

        var lines = backups.Select(b =>
            $"• `{b.Id}` — {b.CreatedAt:yyyy-MM-dd HH:mm} UTC");

        var message = $"📋 **Backups for this server ({backups.Count}):**\n" + string.Join("\n", lines);
        await command.RespondAsync(message, ephemeral: true);
    }

    private async Task HandleRestoreAsync(SocketSlashCommand command)
    {
        var idOption = command.Data.Options.FirstOrDefault(o => o.Name == "id");
        if (idOption?.Value is not string idString || !Guid.TryParse(idString, out var backupId))
        {
            await command.RespondAsync("❌ Invalid or missing backup ID.", ephemeral: true);
            return;
        }

        await _restorationService.ExecuteAsync(command, backupId);
    }
}
