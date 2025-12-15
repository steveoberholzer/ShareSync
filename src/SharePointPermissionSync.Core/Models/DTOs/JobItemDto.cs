namespace SharePointPermissionSync.Core.Models.DTOs;

/// <summary>
/// Data transfer object for individual job item information
/// </summary>
public class JobItemDto
{
    /// <summary>
    /// Database primary key
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Job this item belongs to
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// Unique identifier for this message
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// Type of item (InteractionPermission, InteractionCreation, RemoveUniquePermission)
    /// </summary>
    public string? ItemType { get; set; }

    /// <summary>
    /// Identifier for the item being processed (e.g., InteractionId, FolderId)
    /// </summary>
    public string? ItemIdentifier { get; set; }

    /// <summary>
    /// JSON payload of the message details
    /// </summary>
    public string? Payload { get; set; }

    /// <summary>
    /// Current status (Pending, Processing, Completed, Failed, Requeued)
    /// </summary>
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// Number of times this item has been retried
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Maximum number of retries allowed
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Error message if item failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// When this item was processed
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// When this item was created
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
