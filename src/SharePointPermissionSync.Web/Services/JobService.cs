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
}
