namespace SharePointPermissionSync.Core.Models.Messages;

/// <summary>
/// Base class for all queue messages
/// </summary>
public abstract class QueueMessageBase
{
    /// <summary>
    /// Unique identifier for this message
    /// </summary>
    public Guid MessageId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Identifier for the job this message belongs to
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// Type of operation to perform
    /// </summary>
    public string OperationType { get; set; } = string.Empty;

    /// <summary>
    /// When this message was enqueued
    /// </summary>
    public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Number of times this message has been retried
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Maximum number of retries allowed
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Environment this message targets (DEV, UAT, PROD)
    /// </summary>
    public string Environment { get; set; } = string.Empty;
}
