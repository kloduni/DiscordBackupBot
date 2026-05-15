using Backup.Worker.Handlers;
using Discord;
using Discord.WebSocket;

namespace Backup.Worker.Services;

public class BackupService : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly SlashCommandHandler _slashCommandHandler;
    private readonly ILogger<BackupService> _logger;
    private readonly string _botToken;

    private static readonly SlashCommandProperties[] SlashCommands =
    [
        new SlashCommandBuilder()
            .WithName("backup-create")
            .WithDescription("Creates a full backup of this server's roles, channels, and messages.")
            .Build(),

        new SlashCommandBuilder()
            .WithName("backup-list")
            .WithDescription("Lists all available backups for this server.")
            .Build(),

        new SlashCommandBuilder()
            .WithName("backup-restore")
            .WithDescription("Restores a server backup by its ID.")
            .AddOption("id", ApplicationCommandOptionType.String, "The backup ID to restore.", isRequired: true)
            .Build()
    ];

    public BackupService(
        DiscordSocketClient client,
        SlashCommandHandler slashCommandHandler,
        ILogger<BackupService> logger,
        IConfiguration configuration)
    {
        _client = client;
        _slashCommandHandler = slashCommandHandler;
        _logger = logger;
        _botToken = configuration["Discord:BotToken"]
            ?? throw new InvalidOperationException("Discord:BotToken is not configured.");
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _slashCommandHandler.Register();

        _client.Log += OnLogAsync;
        _client.Ready += OnReadyAsync;

        await _client.LoginAsync(TokenType.Bot, _botToken);
        await _client.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _client.StopAsync();
        await _client.LogoutAsync();
    }

    private async Task OnReadyAsync()
    {
        _logger.LogInformation("Bot is connected as {Username}#{Discriminator}",
            _client.CurrentUser.Username,
            _client.CurrentUser.Discriminator);

        await RegisterSlashCommandsAsync();
    }

    private async Task RegisterSlashCommandsAsync()
    {
        try
        {
            await _client.BulkOverwriteGlobalApplicationCommandsAsync(SlashCommands);
            _logger.LogInformation("Registered {Count} global slash commands.", SlashCommands.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register global slash commands.");
        }
    }

    private Task OnLogAsync(LogMessage message)
    {
        var level = message.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error    => LogLevel.Error,
            LogSeverity.Warning  => LogLevel.Warning,
            LogSeverity.Info     => LogLevel.Information,
            LogSeverity.Verbose  => LogLevel.Debug,
            LogSeverity.Debug    => LogLevel.Trace,
            _                    => LogLevel.Information
        };

        _logger.Log(level, message.Exception, "[Discord] {Source}: {Message}",
            message.Source, message.Message);

        return Task.CompletedTask;
    }
}
