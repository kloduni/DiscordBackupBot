using Backup.Core.Entities;
using Backup.Core.Interfaces;
using Backup.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Backup.Infrastructure.Repositories;

public class BackupRepository : IBackupRepository
{
    private readonly BackupDbContext _context;

    public BackupRepository(BackupDbContext context)
    {
        _context = context;
    }

    public async Task<ServerBackup> CreateBackupAsync(ServerBackup backup)
    {
        backup.Id = Guid.NewGuid();
        backup.CreatedAt = DateTime.UtcNow;
        _context.ServerBackups.Add(backup);
        await _context.SaveChangesAsync();
        return backup;
    }

    public async Task<ServerBackup?> GetBackupAsync(Guid id)
    {
        return await _context.ServerBackups
            .Include(x => x.Roles)
            .Include(x => x.Channels)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<IReadOnlyList<ServerBackup>> GetBackupsByGuildAsync(ulong guildId)
    {
        return await _context.ServerBackups
            .Where(x => x.GuildId == guildId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task AddRolesAsync(IEnumerable<BackupRole> roles)
    {
        await _context.BackupRoles.AddRangeAsync(roles);
    }

    public async Task AddChannelsAsync(IEnumerable<BackupChannel> channels)
    {
        await _context.BackupChannels.AddRangeAsync(channels);
    }

    public async Task AddMessagesAsync(IEnumerable<BackupMessage> messages)
    {
        const int batchSize = 500;
        var batch = new List<BackupMessage>(batchSize);

        foreach (var message in messages)
        {
            batch.Add(message);

            if (batch.Count >= batchSize)
            {
                await _context.BackupMessages.AddRangeAsync(batch);
                await _context.SaveChangesAsync();
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            await _context.BackupMessages.AddRangeAsync(batch);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();
        }
    }

    public async Task<ulong?> GetLastMessageIdAsync(ulong channelId)
    {
        return await _context.BackupMessages
            .Where(m => m.ChannelId == channelId)
            .OrderBy(m => m.MessageId)
            .Select(m => (ulong?)m.MessageId)
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<BackupMessage>> GetMessagesByChannelAsync(ulong channelId)
    {
        return await _context.BackupMessages
            .Where(m => m.ChannelId == channelId)
            .OrderBy(m => m.MessageId)
            .ToListAsync();
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
