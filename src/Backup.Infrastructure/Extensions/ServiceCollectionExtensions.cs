using Backup.Core.Interfaces;
using Backup.Infrastructure.Persistence;
using Backup.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<BackupDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IBackupRepository, BackupRepository>();

        return services;
    }
}
