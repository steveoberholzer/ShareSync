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
            InteractionPermissionMessage perm => $"Interaction:{perm.InteractionId}",
            InteractionCreationMessage create => $"New:{create.InteractionName}",
            RemoveUniquePermissionMessage remove => $"Folder:{remove.FolderId}",
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
    /// Pause a job
    /// </summary>
    public async Task PauseJobAsync(Guid jobId)
    {
        await _jobRepository.PauseJobAsync(jobId);
    }

    /// <summary>
    /// Resume a paused job and republish all paused items
    /// </summary>
    public async Task ResumeJobAsync(Guid jobId)
    {
        // Resume the job status
        await _jobRepository.ResumeJobAsync(jobId);

        // Get all paused items and republish them to the queue
        var pausedItems = await _jobRepository.GetJobItemsAsync(jobId, "Paused");

        if (pausedItems.Any())
        {
            _logger.LogInformation(
                "Republishing {Count} paused items for job {JobId}",
                pausedItems.Count,
                jobId);

            foreach (var item in pausedItems)
            {
                try
                {
                    // Deserialize the payload back to the original message type
                    if (string.IsNullOrEmpty(item.Payload))
                        continue;

                    QueueMessageBase? message = item.ItemType switch
                    {
                        "InteractionPermission" => System.Text.Json.JsonSerializer.Deserialize<InteractionPermissionMessage>(item.Payload),
                        "InteractionCreation" => System.Text.Json.JsonSerializer.Deserialize<InteractionCreationMessage>(item.Payload),
                        "RemoveUniquePermission" => System.Text.Json.JsonSerializer.Deserialize<RemoveUniquePermissionMessage>(item.Payload),
                        _ => null
                    };

                    if (message == null)
                    {
                        _logger.LogWarning("Failed to deserialize paused item {MessageId}", item.MessageId);
                        continue;
                    }

                    // Determine queue name based on message type
                    var queueName = message switch
                    {
                        InteractionPermissionMessage => _configuration["RabbitMQ:Queues:InteractionPermissions"] ?? "sharepoint.interaction.permissions",
                        InteractionCreationMessage => _configuration["RabbitMQ:Queues:InteractionCreation"] ?? "sharepoint.interaction.creation",
                        RemoveUniquePermissionMessage => _configuration["RabbitMQ:Queues:RemovePermissions"] ?? "sharepoint.remove.permissions",
                        _ => throw new InvalidOperationException($"Unknown message type: {message.GetType().Name}")
                    };

                    // Republish to queue
                    await _queueService.PublishAsync(queueName, message);

                    // Update status back to Pending
                    await _jobRepository.UpdateJobItemStatusAsync(item.MessageId, "Pending");

                    _logger.LogInformation("Republished paused item {MessageId} to queue {QueueName}", item.MessageId, queueName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to republish paused item {MessageId}", item.MessageId);
                }
            }
        }
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
}
