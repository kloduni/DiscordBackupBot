namespace Backup.Core.Entities;

public class BackupRole
{
    public Guid Id { get; set; }
    public Guid ServerBackupId { get; set; }
    public ulong RoleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public uint Color { get; set; }
    public int Position { get; set; }
    public ulong Permissions { get; set; }

    public ServerBackup ServerBackup { get; set; } = null!;
}
