using Backup.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backup.Infrastructure.Persistence.Configurations;

public class BackupRoleConfiguration : IEntityTypeConfiguration<BackupRole>
{
    public void Configure(EntityTypeBuilder<BackupRole> builder)
    {
        builder.ToTable("BackupRoles");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RoleId)
            .IsRequired();

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Color)
            .IsRequired();

        builder.Property(x => x.Position)
            .IsRequired();

        builder.Property(x => x.Permissions)
            .IsRequired();
    }
}
