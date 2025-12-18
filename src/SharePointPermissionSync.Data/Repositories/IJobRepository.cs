using SharePointPermissionSync.Data.Entities;

namespace SharePointPermissionSync.Data.Repositories;

/// <summary>
/// Repository interface for managing processing jobs
/// </summary>
public interface IJobRepository
{
    /// <summary>
    /// Create a new processing job
    /// </summary>
    Task<ProcessingJob> CreateJobAsync(ProcessingJob job);

    /// <summary>
    /// Get a job by its GUID
    /// </summary>
    Task<ProcessingJob?> GetJobByIdAsync(Guid jobId);

    /// <summary>
    /// Get all jobs with optional filtering
    /// </summary>
    Task<List<ProcessingJob>> GetJobsAsync(string? status = null, int skip = 0, int take = 50);

    /// <summary>
    /// Update a job's status
    /// </summary>
    Task UpdateJobStatusAsync(Guid jobId, string status, string? errorMessage = null);

    /// <summary>
    /// Increment processed count for a job
    /// </summary>
    Task IncrementProcessedCountAsync(Guid jobId);

    /// <summary>
    /// Increment failed count for a job
    /// </summary>
    Task IncrementFailedCountAsync(Guid jobId);

    /// <summary>
    /// Mark job as started
    /// </summary>
    Task MarkJobAsStartedAsync(Guid jobId);

    /// <summary>
    /// Mark job as completed
    /// </summary>
    Task MarkJobAsCompletedAsync(Guid jobId);

    /// <summary>
    /// Add an item to a job
    /// </summary>
    Task<ProcessingJobItem> AddJobItemAsync(ProcessingJobItem item);

    /// <summary>
    /// Update a job item's status
    /// </summary>
    Task UpdateJobItemStatusAsync(Guid messageId, string status, string? errorMessage = null, int? retryCount = null);

    /// <summary>
    /// Get job items for a specific job
    /// </summary>
    Task<List<ProcessingJobItem>> GetJobItemsAsync(Guid jobId, string? status = null);

    /// <summary>
    /// Get a specific job item by message ID
    /// </summary>
    Task<ProcessingJobItem?> GetJobItemByMessageIdAsync(Guid messageId);

    /// <summary>
    /// Get all job items across all jobs with filtering and pagination
    /// </summary>
    Task<List<ProcessingJobItem>> GetAllJobItemsAsync(
        string? status = null,
        string? itemType = null,
        string? searchTerm = null,
        int skip = 0,
        int take = 100);

    /// <summary>
    /// Get count of all job items with filtering
    /// </summary>
    Task<int> GetAllJobItemsCountAsync(
        string? status = null,
        string? itemType = null,
        string? searchTerm = null);

    /// <summary>
    /// Delete a job item by message ID
    /// </summary>
    Task<bool> DeleteJobItemAsync(Guid messageId);

    /// <summary>
    /// Update job priority
    /// </summary>
    Task UpdateJobPriorityAsync(Guid jobId, string priority);
}
