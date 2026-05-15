using Backup.Infrastructure.Extensions;
using Backup.Worker.Discord;
using Backup.Worker.Handlers;
using Backup.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

builder.Services
    .AddInfrastructure(connectionString)
    .AddDiscordClient()
    .AddSingleton<BackupExtractionService>()
    .AddSingleton<RestorationService>()
    .AddSingleton<SlashCommandHandler>()
    .AddHostedService<BackupService>();

var host = builder.Build();
host.Run();
