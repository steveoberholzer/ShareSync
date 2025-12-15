namespace SharePointPermissionSync.Core.Models.DTOs;

/// <summary>
/// Data transfer object for processing job information
/// </summary>
public class JobDto
{
    /// <summary>
    /// Database primary key
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Unique identifier for this job
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// Type of job (InteractionPermissionSync, InteractionCreation, RemoveUniquePermissions)
    /// </summary>
    public string JobType { get; set; } = string.Empty;

    /// <summary>
    /// Name of the uploaded CSV file
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Email or username of the person who uploaded the file
    /// </summary>
    public string? UploadedBy { get; set; }

    /// <summary>
    /// Target environment (DEV, UAT, PROD)
    /// </summary>
    public string? Environment { get; set; }

    /// <summary>
    /// SharePoint site URL for this job
    /// </summary>
    public string? SiteUrl { get; set; }

    /// <summary>
    /// Total number of items in this job
    /// </summary>
    public int TotalItems { get; set; }

    /// <summary>
    /// Number of items successfully processed
    /// </summary>
    public int ProcessedItems { get; set; }

    /// <summary>
    /// Number of items that failed processing
    /// </summary>
    public int FailedItems { get; set; }

    /// <summary>
    /// Current status of the job (Queued, Processing, Completed, Failed, Paused)
    /// </summary>
    public string Status { get; set; } = "Queued";

    /// <summary>
    /// When this job was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When processing started
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When processing completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Error message if job failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public decimal ProgressPercentage => TotalItems > 0
        ? Math.Round((decimal)ProcessedItems / TotalItems * 100, 2)
        : 0;
}
