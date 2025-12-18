using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SharePointPermissionSync.Core.Configuration;
using SharePointPermissionSync.Core.Models.Messages;

namespace SharePointPermissionSync.Worker.Services;

/// <summary>
/// Service for managing RabbitMQ connections and operations
/// </summary>
public class RabbitMqService : IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RabbitMqService> _logger;
    private IConnection? _connection;
    private IChannel? _channel;
    private bool _disposed;

    public RabbitMqService(
        IConfiguration configuration,
        ILogger<RabbitMqService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Initialize RabbitMQ connection and channel
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _configuration["RabbitMQ:Host"] ?? "localhost",
                Port = int.Parse(_configuration["RabbitMQ:Port"] ?? "5672"),
                UserName = _configuration["RabbitMQ:Username"] ?? "guest",
                Password = _configuration["RabbitMQ:Password"] ?? "guest",
                VirtualHost = _configuration["RabbitMQ:VirtualHost"] ?? "/"
            };

            _logger.LogInformation(
                "Connecting to RabbitMQ at {Host}:{Port}",
                factory.HostName,
                factory.Port);

            _connection = await factory.CreateConnectionAsync();
            _channel = await _connection.CreateChannelAsync();

            _logger.LogInformation("RabbitMQ connection established successfully");

            // Declare queues
            await DeclareQueuesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize RabbitMQ connection");
            throw;
        }
    }

    /// <summary>
    /// Declare all queues with dead letter exchange
    /// </summary>
    private async Task DeclareQueuesAsync()
    {
        if (_channel == null)
            throw new InvalidOperationException("Channel not initialized");

        var deadLetterQueue = _configuration["RabbitMQ:Queues:DeadLetter"] ?? "sharepoint.deadletter";

        // Declare dead letter queue first
        await _channel.QueueDeclareAsync(
            queue: deadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        _logger.LogInformation("Declared dead letter queue: {QueueName}", deadLetterQueue);

        // Declare main queues with dead letter exchange
        var queues = new[]
        {
            _configuration["RabbitMQ:Queues:InteractionPermissions"] ?? "sharepoint.interaction.permissions",
            _configuration["RabbitMQ:Queues:InteractionCreation"] ?? "sharepoint.interaction.creation",
            _configuration["RabbitMQ:Queues:RemovePermissions"] ?? "sharepoint.remove.permissions"
        };

        var arguments = new Dictionary<string, object?>
        {
            { "x-dead-letter-exchange", "" },
            { "x-dead-letter-routing-key", deadLetterQueue },
            { "x-max-priority", 10 }  // Enable priority support (0-10)
        };

        foreach (var queue in queues)
        {
            await _channel.QueueDeclareAsync(
                queue: queue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: arguments);

            _logger.LogInformation("Declared priority queue: {QueueName} (max-priority: 10)", queue);
        }
    }

    /// <summary>
    /// Subscribe to a queue with a message handler
    /// </summary>
    public async Task SubscribeAsync<TMessage>(
        string queueName,
        Func<TMessage, CancellationToken, Task> messageHandler,
        CancellationToken cancellationToken)
        where TMessage : QueueMessageBase
    {
        if (_channel == null)
            throw new InvalidOperationException("Channel not initialized");

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (sender, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var messageJson = Encoding.UTF8.GetString(body);

                _logger.LogDebug("Received message from {QueueName}: {Message}",
                    queueName, messageJson);

                var message = JsonSerializer.Deserialize<TMessage>(messageJson);

                if (message != null)
                {
                    await messageHandler(message, cancellationToken);

                    // Acknowledge the message
                    await _channel.BasicAckAsync(ea.DeliveryTag, false);

                    _logger.LogDebug("Acknowledged message {MessageId}", message.MessageId);
                }
                else
                {
                    _logger.LogWarning("Failed to deserialize message from {QueueName}", queueName);
                    // Reject and don't requeue (send to dead letter)
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from {QueueName}", queueName);

                // Reject and don't requeue (send to dead letter)
                await _channel.BasicNackAsync(ea.DeliveryTag, false, false);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer);

        _logger.LogInformation("Subscribed to queue: {QueueName}", queueName);
    }

    /// <summary>
    /// Publish a message to a queue with priority
    /// </summary>
    public async Task PublishAsync<TMessage>(string queueName, TMessage message, int priority = 5)
        where TMessage : QueueMessageBase
    {
        if (_channel == null)
            throw new InvalidOperationException("Channel not initialized");

        try
        {
            var messageJson = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(messageJson);

            var properties = new BasicProperties
            {
                Persistent = true,
                MessageId = message.MessageId.ToString(),
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                Priority = (byte)Math.Clamp(priority, 0, 10)  // Set priority (0-10)
            };

            await _channel.BasicPublishAsync(
                exchange: "",
                routingKey: queueName,
                mandatory: true,
                basicProperties: properties,
                body: body);

            _logger.LogDebug(
                "Published message {MessageId} to queue {QueueName} with priority {Priority}",
                message.MessageId,
                queueName,
                priority);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish message {MessageId} to queue {QueueName}",
                message.MessageId,
                queueName);
            throw;
        }
    }

    /// <summary>
    /// Publish a message to the dead letter queue
    /// </summary>
    public async Task PublishToDeadLetterAsync<TMessage>(TMessage message)
        where TMessage : QueueMessageBase
    {
        var deadLetterQueue = _configuration["RabbitMQ:Queues:DeadLetter"] ?? "sharepoint.deadletter";
        await PublishAsync(deadLetterQueue, message);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _channel?.Dispose();
        _connection?.Dispose();

        _disposed = true;
        _logger.LogInformation("RabbitMQ connection disposed");
    }
}
