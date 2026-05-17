using Backup.Core.Entities;

namespace Backup.Core.Interfaces;

public interface IBackupRepository
{
    Task<ServerBackup> CreateBackupAsync(ServerBackup backup);
    Task<IReadOnlyList<ServerBackup>> GetAllBackupsAsync();
    Task<ServerBackup?> GetBackupAsync(Guid id);
    Task<IReadOnlyList<ServerBackup>> GetBackupsByGuildAsync(ulong guildId);
    Task AddRolesAsync(IEnumerable<BackupRole> roles);
    Task AddChannelsAsync(IEnumerable<BackupChannel> channels);
    Task AddMessagesAsync(IEnumerable<BackupMessage> messages);
    Task<ulong?> GetLastMessageIdAsync(ulong channelId);
    Task<IReadOnlyList<BackupMessage>> GetMessagesByChannelAsync(ulong channelId, int skip = 0);
    Task<int> GetRestoreProgressAsync(ulong originalChannelId);
    Task UpdateRestoreProgressAsync(ulong originalChannelId, int count);
    Task SaveChangesAsync();
}
