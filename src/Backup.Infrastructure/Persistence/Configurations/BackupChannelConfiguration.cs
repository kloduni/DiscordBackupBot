using Backup.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backup.Infrastructure.Persistence.Configurations;

public class BackupChannelConfiguration : IEntityTypeConfiguration<BackupChannel>
{
    public void Configure(EntityTypeBuilder<BackupChannel> builder)
    {
        builder.ToTable("BackupChannels");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ChannelId)
            .IsRequired();

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Type)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Position)
            .IsRequired();

        builder.Property(x => x.ParentId)
            .IsRequired(false);

        builder.HasMany(x => x.Messages)
            .WithOne(x => x.Channel)
            .HasForeignKey(x => x.BackupChannelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}