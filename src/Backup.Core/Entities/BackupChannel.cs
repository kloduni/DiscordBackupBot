namespace Backup.Core.Entities;

public class BackupChannel
{
    public Guid Id { get; set; }
    public Guid ServerBackupId { get; set; }
    public ulong ChannelId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Position { get; set; }
    public ulong? ParentId { get; set; }

    public ServerBackup ServerBackup { get; set; } = null!;
    public ICollection<BackupMessage> Messages { get; set; } = new List<BackupMessage>();
}
