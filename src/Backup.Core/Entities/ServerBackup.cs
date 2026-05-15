namespace Backup.Core.Entities;

public class ServerBackup
{
    public Guid Id { get; set; }
    public ulong GuildId { get; set; }
    public string GuildName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public ICollection<BackupRole> Roles { get; set; } = new List<BackupRole>();
    public ICollection<BackupChannel> Channels { get; set; } = new List<BackupChannel>();
}
