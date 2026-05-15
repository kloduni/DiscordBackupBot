using Discord;
using Discord.WebSocket;

namespace Backup.Worker.Discord;

public static class DiscordClientFactory
{
    public static IServiceCollection AddDiscordClient(this IServiceCollection services)
    {
        var config = new DiscordSocketConfig
        {
            GatewayIntents =
                GatewayIntents.Guilds |
                GatewayIntents.GuildMessages |
                GatewayIntents.MessageContent |
                GatewayIntents.GuildMembers,
            DefaultRetryMode = RetryMode.AlwaysRetry,
            LogLevel = LogSeverity.Info
        };

        services.AddSingleton(config);
        services.AddSingleton<DiscordSocketClient>();

        return services;
    }
}
