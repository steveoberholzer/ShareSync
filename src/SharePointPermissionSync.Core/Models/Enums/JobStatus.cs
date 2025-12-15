namespace SharePointPermissionSync.Core.Models.Enums;

/// <summary>
/// Status of a processing job
/// </summary>
public enum JobStatus
{
    /// <summary>
    /// Job has been created and queued for processing
    /// </summary>
    Queued,

    /// <summary>
    /// Job is currently being processed
    /// </summary>
    Processing,

    /// <summary>
    /// Job has completed successfully
    /// </summary>
    Completed,

    /// <summary>
    /// Job failed to complete
    /// </summary>
    Failed,

    /// <summary>
    /// Job processing has been paused
    /// </summary>
    Paused
}
