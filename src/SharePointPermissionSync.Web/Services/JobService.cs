using System.Text.Json;
using SharePointPermissionSync.Core.Models;
using SharePointPermissionSync.Core.Models.DTOs;
using SharePointPermissionSync.Core.Models.Messages;
using SharePointPermissionSync.Data.Entities;
using SharePointPermissionSync.Data.Repositories;

namespace SharePointPermissionSync.Web.Services;

/// <summary>
/// Service for managing processing jobs
/// </summary>
public class JobService
{
    private readonly IJobRepository _jobRepository;
    private readonly QueueService _queueService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<JobService> _logger;

    public JobService(
        IJobRepository jobRepository,
        QueueService queueService,
        IConfiguration configuration,
        ILogger<JobService> logger)
    {
        _jobRepository = jobRepository;
        _queueService = queueService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Create a job and publish messages to the queue
    /// </summary>
    public async Task<Guid> CreateJobAsync<TMessage>(
        string jobType,
        string? fileName,
        string? uploadedBy,
        string? environment,
        string? siteUrl,
        IEnumerable<TMessage> messages,
        string priority = "Medium")
        where TMessage : QueueMessageBase
    {
        var jobId = Guid.NewGuid();
        var messageList = messages.ToList();

        _logger.LogInformation(
            "Creating job {JobId} of type {JobType} with {Count} items (Priority: {Priority})",
            jobId,
            jobType,
            messageList.Count,
            priority);

        try
        {
            // Create job in database
            var job = new ProcessingJob
            {
                JobId = jobId,
                JobType = jobType,
                FileName = fileName,
                UploadedBy = uploadedBy,
                Environment = environment,
                SiteUrl = siteUrl,
                TotalItems = messageList.Count,
                ProcessedItems = 0,
                FailedItems = 0,
                Status = "Queued",
                Priority = priority,
                CreatedAt = DateTime.UtcNow
            };

            await _jobRepository.CreateJobAsync(job);

            // Create job items in database
            foreach (var message in messageList)
            {
                // Set job ID on message
                message.JobId = jobId;
                message.Environment = environment ?? "DEV";

                var jobItem = new ProcessingJobItem
                {
                    JobId = jobId,
                    MessageId = message.MessageId,
                    ItemType = jobType,
                    ItemIdentifier = GetItemIdentifier(message),
                    Payload = JsonSerializer.Serialize(message),
                    Status = "Pending",
                    RetryCount = 0,
                    MaxRetries = message.MaxRetries,
                    CreatedAt = DateTime.UtcNow
                };

                await _jobRepository.AddJobItemAsync(jobItem);
            }

            // Publish messages to queue with priority
            var queueName = GetQueueName(jobType);
            var priorityValue = JobPriorityHelper.GetPriorityValue(priority);
            await _queueService.PublishBatchAsync(queueName, messageList, priorityValue);

            _logger.LogInformation(
                "Job {JobId} created successfully with {Count} items published to queue {QueueName} (Priority: {Priority})",
                jobId,
                messageList.Count,
                queueName,
                priority);

            return jobId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to create job {JobId}",
                jobId);
            throw;
        }
    }

    /// <summary>
    /// Get job by ID
    /// </summary>
    public async Task<ProcessingJob?> GetJobAsync(Guid jobId)
    {
        return await _jobRepository.GetJobByIdAsync(jobId);
    }

    /// <summary>
    /// Get all jobs with pagination
    /// </summary>
    public async Task<List<ProcessingJob>> GetJobsAsync(string? status = null, int skip = 0, int take = 50)
    {
        return await _jobRepository.GetJobsAsync(status, skip, take);
    }

    /// <summary>
    /// Get job items for a specific job
    /// </summary>
    public async Task<List<ProcessingJobItem>> GetJobItemsAsync(Guid jobId, string? status = null)
    {
        return await _jobRepository.GetJobItemsAsync(jobId, status);
    }

    private string GetQueueName(string jobType)
    {
        return jobType switch
        {
            "InteractionPermissionSync" => _configuration["RabbitMQ:Queues:InteractionPermissions"]
                ?? "sharepoint.interaction.permissions",
            "InteractionCreation" => _configuration["RabbitMQ:Queues:InteractionCreation"]
                ?? "sharepoint.interaction.creation",
            "RemoveUniquePermissions" => _configuration["RabbitMQ:Queues:RemovePermissions"]
                ?? "sharepoint.remove.permissions",
            _ => throw new ArgumentException($"Unknown job type: {jobType}")
        };
    }

    private string GetItemIdentifier(QueueMessageBase message)
    {
        return message switch
        {
            InteractionPermissionMessage perm => $"{perm.EngagementName} | {perm.ProjectName} | {perm.InteractionName}",
            InteractionCreationMessage create => $"{create.EngagementName} | {create.ProjectName} | {create.InteractionName}",
            RemoveUniquePermissionMessage remove => $"{remove.FolderType} Folder ID: {remove.FolderId}",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Cancel a job and all its pending items
    /// </summary>
    public async Task CancelJobAsync(Guid jobId)
    {
        await _jobRepository.CancelJobAsync(jobId);
    }

    /// <summary>
    /// Restart a job by creating a new job with all items from the original job
    /// </summary>
    /// <returns>The new job ID</returns>
    public async Task<Guid> RestartJobAsync(Guid oldJobId)
    {
        // Get the old job
        var oldJob = await _jobRepository.GetJobByIdAsync(oldJobId);
        if (oldJob == null)
            throw new InvalidOperationException($"Job {oldJobId} not found");

        // Get all items from the old job
        var oldItems = await _jobRepository.GetJobItemsAsync(oldJobId);
        if (!oldItems.Any())
        {
            _logger.LogWarning("Job {JobId} has no items to restart", oldJobId);
            throw new InvalidOperationException($"Job {oldJobId} has no items to restart");
        }

        _logger.LogInformation(
            "Restarting job {OldJobId} with {Count} items",
            oldJobId,
            oldItems.Count);

        // Create new job with same metadata
        var newJob = new ProcessingJob
        {
            JobId = Guid.NewGuid(),
            JobType = oldJob.JobType,
            Status = "Queued",
            Priority = oldJob.Priority,
            TotalItems = oldJob.TotalItems,
            ProcessedItems = 0,
            FailedItems = 0,
            FileName = oldJob.FileName,
            UploadedBy = oldJob.UploadedBy,
            Environment = oldJob.Environment,
            SiteUrl = oldJob.SiteUrl,
            CreatedAt = DateTime.UtcNow
        };

        await _jobRepository.CreateJobAsync(newJob);

        // Republish all items to the queue with new JobId
        int successCount = 0;
        foreach (var item in oldItems)
        {
            try
            {
                // Deserialize the payload back to the original message type
                if (string.IsNullOrEmpty(item.Payload))
                {
                    _logger.LogWarning("Item {MessageId} has empty payload, skipping", item.MessageId);
                    continue;
                }

                var newMessageId = Guid.NewGuid();

                // Handle each message type separately to preserve proper serialization
                switch (item.ItemType)
                {
                    case "InteractionPermissionSync":
                        {
                            var message = JsonSerializer.Deserialize<InteractionPermissionMessage>(item.Payload);
                            if (message == null)
                            {
                                _logger.LogWarning("Failed to deserialize InteractionPermissionMessage {MessageId}", item.MessageId);
                                continue;
                            }

                            message.JobId = newJob.JobId;
                            message.MessageId = newMessageId;

                            var newItem = new ProcessingJobItem
                            {
                                MessageId = newMessageId,
                                JobId = newJob.JobId,
                                ItemType = item.ItemType,
                                Status = "Pending",
                                Payload = JsonSerializer.Serialize(message),
                                ItemIdentifier = item.ItemIdentifier,
                                CreatedAt = DateTime.UtcNow
                            };

                            await _jobRepository.AddJobItemAsync(newItem);

                            var queueName = _configuration["RabbitMQ:Queues:InteractionPermissions"] ?? "sharepoint.interaction.permissions";
                            await _queueService.PublishAsync(queueName, message, GetPriorityValue(newJob.Priority));

                            successCount++;
                            _logger.LogDebug("Republished InteractionPermission item {OldMessageId} as {NewMessageId}",
                                item.MessageId, newMessageId);
                            break;
                        }

                    case "InteractionCreation":
                        {
                            var message = JsonSerializer.Deserialize<InteractionCreationMessage>(item.Payload);
                            if (message == null)
                            {
                                _logger.LogWarning("Failed to deserialize InteractionCreationMessage {MessageId}", item.MessageId);
                                continue;
                            }

                            message.JobId = newJob.JobId;
                            message.MessageId = newMessageId;

                            var newItem = new ProcessingJobItem
                            {
                                MessageId = newMessageId,
                                JobId = newJob.JobId,
                                ItemType = item.ItemType,
                                Status = "Pending",
                                Payload = JsonSerializer.Serialize(message),
                                ItemIdentifier = item.ItemIdentifier,
                                CreatedAt = DateTime.UtcNow
                            };

                            await _jobRepository.AddJobItemAsync(newItem);

                            var queueName = _configuration["RabbitMQ:Queues:InteractionCreation"] ?? "sharepoint.interaction.creation";
                            await _queueService.PublishAsync(queueName, message, GetPriorityValue(newJob.Priority));

                            successCount++;
                            _logger.LogDebug("Republished InteractionCreation item {OldMessageId} as {NewMessageId}",
                                item.MessageId, newMessageId);
                            break;
                        }

                    case "RemoveUniquePermissions":
                        {
                            var message = JsonSerializer.Deserialize<RemoveUniquePermissionMessage>(item.Payload);
                            if (message == null)
                            {
                                _logger.LogWarning("Failed to deserialize RemoveUniquePermissionMessage {MessageId}", item.MessageId);
                                continue;
                            }

                            message.JobId = newJob.JobId;
                            message.MessageId = newMessageId;

                            var newItem = new ProcessingJobItem
                            {
                                MessageId = newMessageId,
                                JobId = newJob.JobId,
                                ItemType = item.ItemType,
                                Status = "Pending",
                                Payload = JsonSerializer.Serialize(message),
                                ItemIdentifier = item.ItemIdentifier,
                                CreatedAt = DateTime.UtcNow
                            };

                            await _jobRepository.AddJobItemAsync(newItem);

                            var queueName = _configuration["RabbitMQ:Queues:RemovePermissions"] ?? "sharepoint.remove.permissions";
                            await _queueService.PublishAsync(queueName, message, GetPriorityValue(newJob.Priority));

                            successCount++;
                            _logger.LogDebug("Republished RemoveUniquePermission item {OldMessageId} as {NewMessageId}",
                                item.MessageId, newMessageId);
                            break;
                        }

                    default:
                        _logger.LogWarning("Unknown ItemType {ItemType} for item {MessageId}, skipping",
                            item.ItemType, item.MessageId);
                        continue;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restart item {MessageId}", item.MessageId);
            }
        }

        _logger.LogInformation(
            "Successfully restarted {SuccessCount}/{TotalCount} items from job {OldJobId} as new job {NewJobId}",
            successCount,
            oldItems.Count,
            oldJobId,
            newJob.JobId);

        return newJob.JobId;
    }

    /// <summary>
    /// Convert priority string to numeric value for RabbitMQ
    /// </summary>
    private int GetPriorityValue(string? priority)
    {
        return priority switch
        {
            "High" => 8,
            "Medium" => 5,
            "Low" => 2,
            _ => 5
        };
    }

    /// <summary>
    /// Update job priority
    /// </summary>
    public async Task UpdateJobPriorityAsync(Guid jobId, string priority)
    {
        await _jobRepository.UpdateJobPriorityAsync(jobId, priority);
    }

    /// <summary>
    /// Update job item payload
    /// </summary>
    public async Task UpdateJobItemPayloadAsync(Guid messageId, string payload)
    {
        await _jobRepository.UpdateJobItemPayloadAsync(messageId, payload);
    }

    /// <summary>
    /// Search job items with text and date range filters
    /// </summary>
    public async Task<List<ProcessingJobItem>> SearchJobItemsAsync(
        string? searchText,
        DateTime? fromDate,
        DateTime? toDate,
        string? status,
        string? itemType,
        int skip,
        int take)
    {
        return await _jobRepository.SearchJobItemsAsync(searchText, fromDate, toDate, status, itemType, skip, take);
    }

    /// <summary>
    /// Get count of search results
    /// </summary>
    public async Task<int> SearchJobItemsCountAsync(
        string? searchText,
        DateTime? fromDate,
        DateTime? toDate,
        string? status,
        string? itemType)
    {
        return await _jobRepository.SearchJobItemsCountAsync(searchText, fromDate, toDate, status, itemType);
    }
}
