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

            // Update job item status to Processing
            await _jobRepository.UpdateJobItemStatusAsync(
                message.MessageId,
                "Processing");

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
}
