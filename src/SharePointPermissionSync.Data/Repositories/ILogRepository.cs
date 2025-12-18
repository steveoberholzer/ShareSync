using SharePointPermissionSync.Data.Entities;

namespace SharePointPermissionSync.Data.Repositories;

/// <summary>
/// Repository interface for managing processing job logs
/// </summary>
public interface ILogRepository
{
    /// <summary>
    /// Add a log entry
    /// </summary>
    Task<ProcessingJobLog> AddLogAsync(ProcessingJobLog log);

    /// <summary>
    /// Add multiple log entries in batch
    /// </summary>
    Task AddLogBatchAsync(IEnumerable<ProcessingJobLog> logs);

    /// <summary>
    /// Get logs for a specific job
    /// </summary>
    Task<List<ProcessingJobLog>> GetLogsAsync(
        Guid jobId,
        Guid? messageId = null,
        string? logLevel = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int skip = 0,
        int take = 100);

    /// <summary>
    /// Get log count for filtering
    /// </summary>
    Task<int> GetLogsCountAsync(
        Guid jobId,
        Guid? messageId = null,
        string? logLevel = null,
        DateTime? startTime = null,
        DateTime? endTime = null);

    /// <summary>
    /// Delete old logs (for cleanup job)
    /// </summary>
    Task<int> DeleteOldLogsAsync(DateTime olderThan);
}
