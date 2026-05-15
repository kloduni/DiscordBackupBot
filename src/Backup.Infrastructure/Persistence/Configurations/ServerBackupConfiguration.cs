using Backup.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backup.Infrastructure.Persistence.Configurations;

public class ServerBackupConfiguration : IEntityTypeConfiguration<ServerBackup>
{
    public void Configure(EntityTypeBuilder<ServerBackup> builder)
    {
        builder.ToTable("ServerBackups");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.GuildId)
            .IsRequired();

        builder.Property(x => x.GuildName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasMany(x => x.Roles)
            .WithOne(x => x.ServerBackup)
            .HasForeignKey(x => x.ServerBackupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Channels)
            .WithOne(x => x.ServerBackup)
            .HasForeignKey(x => x.ServerBackupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
