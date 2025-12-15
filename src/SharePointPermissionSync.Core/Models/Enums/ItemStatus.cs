namespace SharePointPermissionSync.Core.Models.Enums;

/// <summary>
/// Status of an individual processing job item
/// </summary>
public enum ItemStatus
{
    /// <summary>
    /// Item is waiting to be processed
    /// </summary>
    Pending,

    /// <summary>
    /// Item is currently being processed
    /// </summary>
    Processing,

    /// <summary>
    /// Item has been processed successfully
    /// </summary>
    Completed,

    /// <summary>
    /// Item failed to process after all retries
    /// </summary>
    Failed,

    /// <summary>
    /// Item has been requeued for retry
    /// </summary>
    Requeued
}
