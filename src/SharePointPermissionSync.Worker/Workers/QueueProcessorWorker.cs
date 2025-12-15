using SharePointPermissionSync.Worker.Services;

namespace SharePointPermissionSync.Worker.Workers;

/// <summary>
/// Background worker that processes messages from RabbitMQ queue
/// </summary>
public class QueueProcessorWorker : BackgroundService
{
    private readonly ILogger<QueueProcessorWorker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private RabbitMqService? _rabbitMqService;
    private QueueConsumer? _queueConsumer;

    public QueueProcessorWorker(
        ILogger<QueueProcessorWorker> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Queue Processor Worker started at: {Time}",
            DateTimeOffset.Now);

        _logger.LogInformation(
            "Environment: {Environment}",
            _configuration["Environment"]);

        _logger.LogInformation(
            "RabbitMQ Host: {Host}:{Port}",
            _configuration["RabbitMQ:Host"],
            _configuration["RabbitMQ:Port"]);

        try
        {
            // Create a scope for scoped services
            using var scope = _serviceProvider.CreateScope();

            // Initialize RabbitMQ service
            _rabbitMqService = scope.ServiceProvider.GetRequiredService<RabbitMqService>();
            await _rabbitMqService.InitializeAsync();

            _logger.LogInformation("RabbitMQ connection initialized successfully");

            // Initialize queue consumer
            _queueConsumer = scope.ServiceProvider.GetRequiredService<QueueConsumer>();
            await _queueConsumer.StartConsumingAsync(stoppingToken);

            _logger.LogInformation(
                "Queue Processor Worker is ready to process messages from all queues");

            // Keep the worker running - messages are processed via RabbitMQ events
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

                var throttleStats = scope.ServiceProvider
                    .GetRequiredService<ThrottleManager>()
                    .GetStats();

                _logger.LogInformation(
                    "Worker health check - Current delay: {Delay}ms, Success count: {SuccessCount}, Throttle count: {ThrottleCount}",
                    throttleStats.CurrentDelay,
                    throttleStats.SuccessCount,
                    throttleStats.ThrottleCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Queue Processor Worker");
            throw;
        }
        finally
        {
            _logger.LogInformation(
                "Queue Processor Worker stopped at: {Time}",
                DateTimeOffset.Now);
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Queue Processor Worker is stopping");

        // Dispose RabbitMQ connection
        _rabbitMqService?.Dispose();

        _logger.LogInformation("RabbitMQ connection closed gracefully");

        await base.StopAsync(stoppingToken);
    }
}
