namespace Backup.Core.Entities;

public class BackupMessage
{
    public Guid Id { get; set; }
    public ulong ChannelId { get; set; }
    public ulong MessageId { get; set; }
    public string Author { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }

    public BackupChannel Channel { get; set; } = null!;
}
