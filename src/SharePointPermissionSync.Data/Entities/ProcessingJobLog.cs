namespace SharePointPermissionSync.Data.Entities;

/// <summary>
/// Represents a log entry for a processing job
/// </summary>
public class ProcessingJobLog
{
    /// <summary>
    /// Database primary key
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Job this log belongs to
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// Optional message ID if log is specific to an item
    /// </summary>
    public Guid? MessageId { get; set; }

    /// <summary>
    /// Log level (Debug, Info, Warning, Error)
    /// </summary>
    public string LogLevel { get; set; } = "Info";

    /// <summary>
    /// Log message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Additional details as JSON
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Source of the log (e.g., "MessageProcessor", "InteractionPermissionHandler")
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// When this log was created
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Navigation properties
    /// <summary>
    /// Parent job
    /// </summary>
    public virtual ProcessingJob? Job { get; set; }
}
