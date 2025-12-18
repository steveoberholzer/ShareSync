using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using SharePointPermissionSync.Core.Models.Messages;

namespace SharePointPermissionSync.Web.Services;

/// <summary>
/// Service for publishing messages to RabbitMQ queues
/// </summary>
public class QueueService : IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<QueueService> _logger;
    private IConnection? _connection;
    private IChannel? _channel;
    private bool _disposed;

    public QueueService(
        IConfiguration configuration,
        ILogger<QueueService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Initialize RabbitMQ connection
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_connection != null && _connection.IsOpen)
            return;

        var factory = new ConnectionFactory
        {
            HostName = _configuration["RabbitMQ:Host"] ?? "localhost",
            Port = int.Parse(_configuration["RabbitMQ:Port"] ?? "5672"),
            UserName = _configuration["RabbitMQ:Username"] ?? "guest",
            Password = _configuration["RabbitMQ:Password"] ?? "guest",
            VirtualHost = _configuration["RabbitMQ:VirtualHost"] ?? "/"
        };

        _connection = await factory.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();

        _logger.LogInformation("RabbitMQ connection established");
    }

    /// <summary>
    /// Publish a message to a queue
    /// </summary>
    public async Task PublishAsync<TMessage>(string queueName, TMessage message)
        where TMessage : QueueMessageBase
    {
        if (_channel == null)
            await InitializeAsync();

        try
        {
            var messageJson = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(messageJson);

            var properties = new BasicProperties
            {
                Persistent = true,
                MessageId = message.MessageId.ToString(),
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            await _channel!.BasicPublishAsync(
                exchange: "",
                routingKey: queueName,
                mandatory: true,
                basicProperties: properties,
                body: body);

            _logger.LogInformation(
                "Published message {MessageId} to queue {QueueName} (Job: {JobId})",
                message.MessageId,
                queueName,
                message.JobId);
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
    /// Publish multiple messages in a batch
    /// </summary>
    public async Task PublishBatchAsync<TMessage>(string queueName, IEnumerable<TMessage> messages)
        where TMessage : QueueMessageBase
    {
        foreach (var message in messages)
        {
            await PublishAsync(queueName, message);
        }

        _logger.LogInformation(
            "Published {Count} messages to queue {QueueName}",
            messages.Count(),
            queueName);
    }

    /// <summary>
    /// Publish a raw JSON message to a queue (for retry scenarios)
    /// </summary>
    public async Task PublishMessageAsync(string queueName, string messageJson)
    {
        if (_channel == null)
            await InitializeAsync();

        try
        {
            var body = Encoding.UTF8.GetBytes(messageJson);

            var properties = new BasicProperties
            {
                Persistent = true,
                MessageId = Guid.NewGuid().ToString(),
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            await _channel!.BasicPublishAsync(
                exchange: "",
                routingKey: queueName,
                mandatory: true,
                basicProperties: properties,
                body: body);

            _logger.LogInformation(
                "Published raw message to queue {QueueName}",
                queueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish raw message to queue {QueueName}",
                queueName);
            throw;
        }
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
