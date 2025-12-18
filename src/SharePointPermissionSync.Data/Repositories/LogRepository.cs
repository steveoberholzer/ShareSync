using Microsoft.EntityFrameworkCore;
using SharePointPermissionSync.Data.Entities;

namespace SharePointPermissionSync.Data.Repositories;

/// <summary>
/// Repository for managing processing job logs
/// </summary>
public class LogRepository : ILogRepository
{
    private readonly ScyneShareContext _context;

    public LogRepository(ScyneShareContext context)
    {
        _context = context;
    }

    public async Task<ProcessingJobLog> AddLogAsync(ProcessingJobLog log)
    {
        _context.ProcessingJobLogs.Add(log);
        await _context.SaveChangesAsync();
        return log;
    }

    public async Task AddLogBatchAsync(IEnumerable<ProcessingJobLog> logs)
    {
        _context.ProcessingJobLogs.AddRange(logs);
        await _context.SaveChangesAsync();
    }

    public async Task<List<ProcessingJobLog>> GetLogsAsync(
        Guid jobId,
        Guid? messageId = null,
        string? logLevel = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int skip = 0,
        int take = 100)
    {
        var query = _context.ProcessingJobLogs.Where(l => l.JobId == jobId);

        if (messageId.HasValue)
        {
            query = query.Where(l => l.MessageId == messageId.Value);
        }

        if (!string.IsNullOrEmpty(logLevel))
        {
            query = query.Where(l => l.LogLevel == logLevel);
        }

        if (startTime.HasValue)
        {
            query = query.Where(l => l.Timestamp >= startTime.Value);
        }

        if (endTime.HasValue)
        {
            query = query.Where(l => l.Timestamp <= endTime.Value);
        }

        return await query
            .OrderByDescending(l => l.Timestamp)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<int> GetLogsCountAsync(
        Guid jobId,
        Guid? messageId = null,
        string? logLevel = null,
        DateTime? startTime = null,
        DateTime? endTime = null)
    {
        var query = _context.ProcessingJobLogs.Where(l => l.JobId == jobId);

        if (messageId.HasValue)
        {
            query = query.Where(l => l.MessageId == messageId.Value);
        }

        if (!string.IsNullOrEmpty(logLevel))
        {
            query = query.Where(l => l.LogLevel == logLevel);
        }

        if (startTime.HasValue)
        {
            query = query.Where(l => l.Timestamp >= startTime.Value);
        }

        if (endTime.HasValue)
        {
            query = query.Where(l => l.Timestamp <= endTime.Value);
        }

        return await query.CountAsync();
    }

    public async Task<int> DeleteOldLogsAsync(DateTime olderThan)
    {
        var logsToDelete = await _context.ProcessingJobLogs
            .Where(l => l.Timestamp < olderThan)
            .ToListAsync();

        _context.ProcessingJobLogs.RemoveRange(logsToDelete);
        await _context.SaveChangesAsync();

        return logsToDelete.Count;
    }
}
