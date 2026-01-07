using Microsoft.SharePoint.Client;
using SharePointPermissionSync.Core.Models.Messages;
using SharePointPermissionSync.Data.Repositories;
using SharePointPermissionSync.Worker.Handlers;

namespace SharePointPermissionSync.Worker.Services;

/// <summary>
/// Processes queue messages and routes them to appropriate handlers
/// </summary>
public class MessageProcessor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IJobRepository _jobRepository;
    private readonly ThrottleManager _throttleManager;
    private readonly RabbitMqService _rabbitMqService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MessageProcessor> _logger;

    public MessageProcessor(
        IServiceProvider serviceProvider,
        IJobRepository jobRepository,
        ThrottleManager throttleManager,
        RabbitMqService rabbitMqService,
        IConfiguration configuration,
        ILogger<MessageProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _jobRepository = jobRepository;
        _throttleManager = throttleManager;
        _rabbitMqService = rabbitMqService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Process a queue message
    /// </summary>
    public async Task ProcessMessageAsync(
        QueueMessageBase message,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation(
                "Processing message {MessageId} (Type: {OperationType}, Job: {JobId})",
                message.MessageId,
                message.OperationType,
                message.JobId);

            // Check if job is cancelled or paused (fetch fresh status from database)
            var jobStatus = await _jobRepository.GetJobStatusAsync(message.JobId);

            if (jobStatus == "Cancelled")
            {
                _logger.LogWarning(
                    "Skipping message {MessageId} - Job {JobId} is cancelled",
                    message.MessageId,
                    message.JobId);

                await _jobRepository.UpdateJobItemStatusAsync(
                    message.MessageId,
                    "Cancelled",
                    "Job was cancelled");

                return; // Skip processing
            }

            if (jobStatus == "Paused")
            {
                _logger.LogInformation(
                    "Skipping message {MessageId} - Job {JobId} is paused. Message will be requeued.",
                    message.MessageId,
                    message.JobId);

                // Requeue the message for later processing
                var queueName = GetQueueNameForMessage(message);
                await _rabbitMqService.PublishAsync(queueName, message);

                return; // Skip processing
            }

            // Update job item status to Processing
            await _jobRepository.UpdateJobItemStatusAsync(
                message.MessageId,
                "Processing");

            // Mark job as started if this is the first item being processed
            await EnsureJobStartedAsync(message.JobId);

            // Route to appropriate handler
            OperationResult result = message switch
            {
                InteractionPermissionMessage permMsg =>
                    await HandleMessageAsync<InteractionPermissionHandler, InteractionPermissionMessage>(permMsg, cancellationToken),

                InteractionCreationMessage createMsg =>
                    await HandleMessageAsync<InteractionCreationHandler, InteractionCreationMessage>(createMsg, cancellationToken),

                RemoveUniquePermissionMessage removeMsg =>
                    await HandleMessageAsync<RemoveUniquePermissionHandler, RemoveUniquePermissionMessage>(removeMsg, cancellationToken),

                _ => OperationResult.FailureResult($"Unknown message type: {message.GetType().Name}")
            };

            // Process result
            if (result.Success)
            {
                await HandleSuccess(message);
            }
            else
            {
                await HandleFailure(message, result.ErrorMessage ?? "Unknown error", result.ErrorCode);
            }

            // Apply throttling delay
            await Task.Delay(_throttleManager.CurrentDelay, cancellationToken);

            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation(
                "Completed processing message {MessageId} in {ElapsedMs}ms (Success: {Success})",
                message.MessageId,
                elapsed.TotalMilliseconds,
                result.Success);
        }
        catch (Exception ex) when (IsThrottlingException(ex))
        {
            _logger.LogWarning(
                "Throttling detected for message {MessageId}. Increasing delay.",
                message.MessageId);

            _throttleManager.ReportThrottling();
            await HandleFailure(message, "SharePoint throttling (429)", 429);

            // Apply longer delay for throttling
            await Task.Delay(_throttleManager.CurrentDelay * 2, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error processing message {MessageId}",
                message.MessageId);
            await HandleFailure(message, ex.Message);
        }
    }

    private async Task<OperationResult> HandleMessageAsync<THandler, TMessage>(
        TMessage message,
        CancellationToken cancellationToken)
        where THandler : IOperationHandler<TMessage>
        where TMessage : QueueMessageBase
    {
        var handler = _serviceProvider.GetRequiredService<THandler>();
        return await handler.HandleAsync(message, cancellationToken);
    }

    private async Task HandleSuccess(QueueMessageBase message)
    {
        // Mark item as completed
        await _jobRepository.UpdateJobItemStatusAsync(
            message.MessageId,
            "Completed");

        // Increment processed count
        await _jobRepository.IncrementProcessedCountAsync(message.JobId);

        // Check if all items in the job are complete
        await CheckAndUpdateJobStatusAsync(message.JobId);

        // Report success to throttle manager
        _throttleManager.ReportSuccess();

        _logger.LogInformation(
            "Successfully processed message {MessageId}",
            message.MessageId);
    }

    private async Task HandleFailure(QueueMessageBase message, string errorMessage, int errorCode = 0)
    {
        message.RetryCount++;

        if (message.RetryCount < message.MaxRetries)
        {
            // Requeue for retry
            await _jobRepository.UpdateJobItemStatusAsync(
                message.MessageId,
                "Requeued",
                errorMessage,
                message.RetryCount);

            _logger.LogWarning(
                "Message {MessageId} failed (attempt {RetryCount}/{MaxRetries}): {ErrorMessage}. Will retry.",
                message.MessageId,
                message.RetryCount,
                message.MaxRetries,
                errorMessage);

            // Republish to appropriate queue for retry
            var queueName = GetQueueNameForMessage(message);
            await _rabbitMqService.PublishAsync(queueName, message);

            _logger.LogInformation(
                "Republished message {MessageId} to queue {QueueName} for retry",
                message.MessageId,
                queueName);
        }
        else
        {
            // Max retries exceeded - mark as failed and send to dead letter
            await _jobRepository.UpdateJobItemStatusAsync(
                message.MessageId,
                "Failed",
                errorMessage,
                message.RetryCount);

            await _jobRepository.IncrementFailedCountAsync(message.JobId);

            // Check if all items in the job are complete (including failures)
            await CheckAndUpdateJobStatusAsync(message.JobId);

            await _rabbitMqService.PublishToDeadLetterAsync(message);

            _logger.LogError(
                "Message {MessageId} failed permanently after {RetryCount} attempts: {ErrorMessage}. Sent to dead letter queue.",
                message.MessageId,
                message.RetryCount,
                errorMessage);
        }
    }

    private string GetQueueNameForMessage(QueueMessageBase message)
    {
        return message switch
        {
            InteractionPermissionMessage => _configuration["RabbitMQ:Queues:InteractionPermissions"]
                ?? "sharepoint.interaction.permissions",
            InteractionCreationMessage => _configuration["RabbitMQ:Queues:InteractionCreation"]
                ?? "sharepoint.interaction.creation",
            RemoveUniquePermissionMessage => _configuration["RabbitMQ:Queues:RemovePermissions"]
                ?? "sharepoint.remove.permissions",
            _ => throw new ArgumentException($"Unknown message type: {message.GetType().Name}")
        };
    }

    private bool IsThrottlingException(Exception ex)
    {
        // Check for SharePoint throttling exceptions
        if (ex is ServerException serverEx)
        {
            return serverEx.ServerErrorCode == -2147429894 ||
                   serverEx.Message.Contains("429") ||
                   serverEx.Message.Contains("too many requests", StringComparison.OrdinalIgnoreCase);
        }

        if (ex.Message.Contains("429") || ex.Message.Contains("throttl", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Ensure job is marked as started when first item begins processing
    /// </summary>
    private async Task EnsureJobStartedAsync(Guid jobId)
    {
        try
        {
            var job = await _jobRepository.GetJobByIdAsync(jobId);
            if (job != null && job.Status == "Queued")
            {
                await _jobRepository.MarkJobAsStartedAsync(jobId);
                _logger.LogInformation(
                    "Job {JobId} marked as Processing (first item started)",
                    jobId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to mark job {JobId} as started (non-critical)",
                jobId);
        }
    }

    /// <summary>
    /// Check if all job items are complete and update job status accordingly
    /// </summary>
    private async Task CheckAndUpdateJobStatusAsync(Guid jobId)
    {
        try
        {
            var job = await _jobRepository.GetJobByIdAsync(jobId);
            if (job == null)
            {
                _logger.LogWarning("Job {JobId} not found when checking completion status", jobId);
                return;
            }

            // Get all job items
            var allItems = await _jobRepository.GetJobItemsAsync(jobId);
            if (!allItems.Any())
            {
                _logger.LogWarning("Job {JobId} has no items", jobId);
                return;
            }

            // Count items by status
            var totalItems = allItems.Count;
            var completedItems = allItems.Count(i => i.Status == "Completed");
            var failedItems = allItems.Count(i => i.Status == "Failed");
            var pendingItems = allItems.Count(i => i.Status == "Pending" || i.Status == "Processing" || i.Status == "Requeued");

            _logger.LogInformation(
                "Job {JobId} status check: {Completed}/{Total} completed, {Failed} failed, {Pending} pending",
                jobId, completedItems, totalItems, failedItems, pendingItems);

            // If all items are complete (succeeded or failed), mark job as complete
            if (pendingItems == 0)
            {
                if (failedItems == totalItems)
                {
                    // All items failed
                    await _jobRepository.UpdateJobStatusAsync(
                        jobId,
                        "Failed",
                        $"All {totalItems} items failed");

                    _logger.LogWarning(
                        "Job {JobId} marked as Failed - all items failed",
                        jobId);
                }
                else if (failedItems > 0)
                {
                    // Some items failed
                    await _jobRepository.MarkJobAsCompletedAsync(jobId);
                    await _jobRepository.UpdateJobStatusAsync(
                        jobId,
                        "Completed with Errors",
                        $"{failedItems} of {totalItems} items failed");

                    _logger.LogWarning(
                        "Job {JobId} marked as Completed with Errors - {Failed}/{Total} items failed",
                        jobId, failedItems, totalItems);
                }
                else
                {
                    // All items succeeded
                    await _jobRepository.MarkJobAsCompletedAsync(jobId);

                    _logger.LogInformation(
                        "Job {JobId} marked as Completed - all {Total} items succeeded",
                        jobId, totalItems);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to update job {JobId} completion status (non-critical)",
                jobId);
        }
    }
}
