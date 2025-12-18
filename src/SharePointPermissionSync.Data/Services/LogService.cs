using Microsoft.Extensions.Logging;
using SharePointPermissionSync.Data.Entities;
using SharePointPermissionSync.Data.Repositories;
using System.Collections.Concurrent;
using System.Text.Json;

namespace SharePointPermissionSync.Data.Services;

/// <summary>
/// Service for writing structured logs to database with batching
/// </summary>
public class LogService : IDisposable
{
    private readonly ILogRepository _logRepository;
    private readonly ILogger<LogService> _logger;
    private readonly ConcurrentQueue<ProcessingJobLog> _logQueue;
    private readonly Timer _flushTimer;
    private readonly SemaphoreSlim _flushLock;
    private bool _disposed;

    public LogService(
        ILogRepository logRepository,
        ILogger<LogService> logger)
    {
        _logRepository = logRepository;
        _logger = logger;
        _logQueue = new ConcurrentQueue<ProcessingJobLog>();
        _flushLock = new SemaphoreSlim(1, 1);

        // Flush logs every 5 seconds
        _flushTimer = new Timer(async _ => await FlushLogsAsync(), null,
            TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Log a message for a job
    /// </summary>
    public void Log(Guid jobId, string level, string message, string? source = null,
        Guid? messageId = null, object? details = null)
    {
        var log = new ProcessingJobLog
        {
            JobId = jobId,
            MessageId = messageId,
            LogLevel = level,
            Message = message,
            Source = source,
            Details = details != null ? JsonSerializer.Serialize(details) : null,
            Timestamp = DateTime.UtcNow
        };

        _logQueue.Enqueue(log);

        // Also log to console/file via standard logger
        var logLevel = level switch
        {
            "Debug" => LogLevel.Debug,
            "Info" => LogLevel.Information,
            "Warning" => LogLevel.Warning,
            "Error" => LogLevel.Error,
            _ => LogLevel.Information
        };

        _logger.Log(logLevel, "[Job:{JobId}] {Message}", jobId, message);
    }

    /// <summary>
    /// Convenience methods for different log levels
    /// </summary>
    public void LogDebug(Guid jobId, string message, string? source = null,
        Guid? messageId = null, object? details = null)
        => Log(jobId, "Debug", message, source, messageId, details);

    public void LogInfo(Guid jobId, string message, string? source = null,
        Guid? messageId = null, object? details = null)
        => Log(jobId, "Info", message, source, messageId, details);

    public void LogWarning(Guid jobId, string message, string? source = null,
        Guid? messageId = null, object? details = null)
        => Log(jobId, "Warning", message, source, messageId, details);

    public void LogError(Guid jobId, string message, string? source = null,
        Guid? messageId = null, object? details = null)
        => Log(jobId, "Error", message, source, messageId, details);

    /// <summary>
    /// Flush all pending logs to database
    /// </summary>
    public async Task FlushLogsAsync()
    {
        if (_logQueue.IsEmpty)
            return;

        await _flushLock.WaitAsync();
        try
        {
            var logsToWrite = new List<ProcessingJobLog>();
            while (_logQueue.TryDequeue(out var log) && logsToWrite.Count < 1000)
            {
                logsToWrite.Add(log);
            }

            if (logsToWrite.Any())
            {
                await _logRepository.AddLogBatchAsync(logsToWrite);
                _logger.LogDebug("Flushed {Count} logs to database", logsToWrite.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flushing logs to database");
        }
        finally
        {
            _flushLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _flushTimer?.Dispose();
        FlushLogsAsync().GetAwaiter().GetResult();
        _flushLock?.Dispose();

        _disposed = true;
    }
}
