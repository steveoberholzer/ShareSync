using SharePointPermissionSync.Core.Models.Messages;

namespace SharePointPermissionSync.Worker.Services;

/// <summary>
/// Manages consuming messages from multiple RabbitMQ queues
/// </summary>
public class QueueConsumer
{
    private readonly RabbitMqService _rabbitMqService;
    private readonly MessageProcessor _messageProcessor;
    private readonly IConfiguration _configuration;
    private readonly ILogger<QueueConsumer> _logger;

    public QueueConsumer(
        RabbitMqService rabbitMqService,
        MessageProcessor messageProcessor,
        IConfiguration configuration,
        ILogger<QueueConsumer> logger)
    {
        _rabbitMqService = rabbitMqService;
        _messageProcessor = messageProcessor;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Start consuming from all queues
    /// </summary>
    public async Task StartConsumingAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting queue consumers");

        // Subscribe to Interaction Permissions queue
        var permissionsQueue = _configuration["RabbitMQ:Queues:InteractionPermissions"]
            ?? "sharepoint.interaction.permissions";

        await _rabbitMqService.SubscribeAsync<InteractionPermissionMessage>(
            permissionsQueue,
            async (message, ct) => await HandleMessageAsync(message, ct),
            cancellationToken);

        // Subscribe to Interaction Creation queue
        var creationQueue = _configuration["RabbitMQ:Queues:InteractionCreation"]
            ?? "sharepoint.interaction.creation";

        await _rabbitMqService.SubscribeAsync<InteractionCreationMessage>(
            creationQueue,
            async (message, ct) => await HandleMessageAsync(message, ct),
            cancellationToken);

        // Subscribe to Remove Permissions queue
        var removeQueue = _configuration["RabbitMQ:Queues:RemovePermissions"]
            ?? "sharepoint.remove.permissions";

        await _rabbitMqService.SubscribeAsync<RemoveUniquePermissionMessage>(
            removeQueue,
            async (message, ct) => await HandleMessageAsync(message, ct),
            cancellationToken);

        _logger.LogInformation("All queue consumers started successfully");
    }

    /// <summary>
    /// Handle a message by routing to MessageProcessor
    /// </summary>
    private async Task HandleMessageAsync(
        QueueMessageBase message,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing message {MessageId} of type {MessageType}",
            message.MessageId,
            message.GetType().Name);

        try
        {
            await _messageProcessor.ProcessMessageAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Fatal error processing message {MessageId}",
                message.MessageId);

            // Message will be rejected and sent to dead letter queue by RabbitMqService
            throw;
        }
    }
}
