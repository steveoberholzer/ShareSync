using System;
using System.Text;
using Newtonsoft.Json;
using RabbitMQ.Client;

namespace Tecala.SMO.ShareSync.Services
{
    /// <summary>
    /// Service for publishing messages to RabbitMQ
    /// </summary>
    public class QueueService : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly string _username;
        private readonly string _password;
        private readonly string _virtualHost;
        private readonly ILogger _logger;
        private IConnection _connection;
        private IModel _channel;

        public QueueService(string host, int port, string username, string password, string virtualHost, ILogger logger)
        {
            _host = host;
            _port = port;
            _username = username;
            _password = password;
            _virtualHost = virtualHost;
            _logger = logger;
        }

        /// <summary>
        /// Initialize RabbitMQ connection
        /// </summary>
        public void Initialize()
        {
            if (_connection != null && _connection.IsOpen)
                return;

            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _host,
                    Port = _port,
                    UserName = _username,
                    Password = _password,
                    VirtualHost = _virtualHost
                };

                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                _logger.LogInformation($"Connected to RabbitMQ at {_host}:{_port}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to RabbitMQ");
                throw;
            }
        }

        /// <summary>
        /// Publish a message to a queue with priority
        /// </summary>
        public void PublishMessage(string queueName, object message, int priority = 5)
        {
            try
            {
                if (_channel == null)
                    Initialize();

                string messageJson = JsonConvert.SerializeObject(message);
                byte[] body = Encoding.UTF8.GetBytes(messageJson);

                var properties = _channel.CreateBasicProperties();
                properties.Persistent = true;
                properties.Priority = (byte)Math.Min(Math.Max(priority, 0), 10);

                _channel.BasicPublish(
                    exchange: "",
                    routingKey: queueName,
                    basicProperties: properties,
                    body: body);

                _logger.LogInformation($"Published message to queue {queueName} with priority {priority}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to publish message to queue {queueName}");
                throw;
            }
        }

        public void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
        }
    }
}
