using Backup.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backup.Infrastructure.Persistence.Configurations;

public class BackupMessageConfiguration : IEntityTypeConfiguration<BackupMessage>
{
    public void Configure(EntityTypeBuilder<BackupMessage> builder)
    {
        builder.ToTable("BackupMessages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ChannelId)
            .IsRequired();

        builder.Property(x => x.MessageId)
            .IsRequired();

        builder.HasIndex(x => x.MessageId)
            .IsUnique();

        builder.Property(x => x.Author)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.AvatarUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.Content)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(x => x.Timestamp)
            .IsRequired();
    }
}
