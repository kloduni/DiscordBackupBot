using Backup.Core.Entities;
using Backup.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Backup.Infrastructure.Persistence;

public class BackupDbContext : DbContext
{
    public BackupDbContext(DbContextOptions<BackupDbContext> options) : base(options) { }

    public DbSet<ServerBackup> ServerBackups => Set<ServerBackup>();
    public DbSet<BackupRole> BackupRoles => Set<BackupRole>();
    public DbSet<BackupChannel> BackupChannels => Set<BackupChannel>();
    public DbSet<BackupMessage> BackupMessages => Set<BackupMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ServerBackupConfiguration());
        modelBuilder.ApplyConfiguration(new BackupRoleConfiguration());
        modelBuilder.ApplyConfiguration(new BackupChannelConfiguration());
        modelBuilder.ApplyConfiguration(new BackupMessageConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}
