namespace Backup.Core.Entities;

public class ChannelRestoreProgress
{
    public ulong OriginalChannelId { get; set; }
    public int RestoredCount { get; set; }
}
