using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using SharePointPermissionSync.Data;

namespace SharePointPermissionSync.Worker.Services;

/// <summary>
/// Validates all required dependencies and configurations at startup
/// </summary>
public class StartupValidator
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<StartupValidator> _logger;
    private readonly IServiceProvider _serviceProvider;

    public StartupValidator(
        IConfiguration configuration,
        ILogger<StartupValidator> logger,
        IServiceProvider serviceProvider)
    {
        _configuration = configuration;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Validates all startup requirements
    /// Returns true if all validations pass, false otherwise
    /// </summary>
    public async Task<bool> ValidateStartupAsync()
    {
        _logger.LogInformation("=== SharePoint Permission Sync Worker - Startup Validation ===");
        _logger.LogInformation("Starting at: {Time}", DateTimeOffset.Now);

        try
        {
            // Step 1: Validate and log configuration settings
            if (!ValidateSettings())
            {
                _logger.LogCritical("Configuration validation failed. Service cannot start.");
                return false;
            }

            // Step 2: Check if RabbitMQ is installed and running
            if (!await ValidateRabbitMqInstallationAsync())
            {
                _logger.LogCritical("RabbitMQ validation failed. Service cannot start.");
                _logger.LogCritical("Please install RabbitMQ from: https://www.rabbitmq.com/download.html");
                _logger.LogCritical("After installation, ensure the RabbitMQ service is running.");
                return false;
            }

            // Step 3: Validate database connection and tables
            if (!await ValidateDatabaseAsync())
            {
                _logger.LogCritical("Database validation failed. Service cannot start.");
                return false;
            }

            // Step 4: Initialize RabbitMQ queues
            if (!await InitializeRabbitMqQueuesAsync())
            {
                _logger.LogCritical("RabbitMQ queue initialization failed. Service cannot start.");
                return false;
            }

            _logger.LogInformation("=== All startup validations passed successfully ===");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Unexpected error during startup validation");
            return false;
        }
    }

    /// <summary>
    /// Validates configuration settings and logs them
    /// </summary>
    private bool ValidateSettings()
    {
        _logger.LogInformation("--- Validating Configuration Settings ---");

        var environment = _configuration["Environment"];
        _logger.LogInformation("Environment: {Environment}", environment ?? "NOT SET");

        if (string.IsNullOrWhiteSpace(environment))
        {
            _logger.LogError("Environment setting is not configured");
            return false;
        }

        // Validate connection string
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogError("Database connection string 'DefaultConnection' is not configured");
            return false;
        }

        // Log sanitized connection string (hide sensitive parts)
        var sanitizedConnectionString = SanitizeConnectionString(connectionString);
        _logger.LogInformation("Database Connection: {ConnectionString}", sanitizedConnectionString);

        // Validate RabbitMQ settings
        var rabbitMqHost = _configuration["RabbitMQ:Host"];
        var rabbitMqPort = _configuration["RabbitMQ:Port"];
        var rabbitMqUsername = _configuration["RabbitMQ:Username"];

        _logger.LogInformation("RabbitMQ Host: {Host}", rabbitMqHost ?? "NOT SET");
        _logger.LogInformation("RabbitMQ Port: {Port}", rabbitMqPort ?? "NOT SET");
        _logger.LogInformation("RabbitMQ Username: {Username}", rabbitMqUsername ?? "NOT SET");

        if (string.IsNullOrWhiteSpace(rabbitMqHost))
        {
            _logger.LogError("RabbitMQ Host is not configured");
            return false;
        }

        // Validate SharePoint settings for current environment
        var tenantId = _configuration[$"SharePoint:{environment}:TenantId"];
        var clientId = _configuration[$"SharePoint:{environment}:ClientId"];
        var thumbprint = _configuration[$"SharePoint:{environment}:CertificateThumbprint"];

        _logger.LogInformation("SharePoint TenantId: {TenantId}",
            string.IsNullOrWhiteSpace(tenantId) ? "NOT SET" : "***configured***");
        _logger.LogInformation("SharePoint ClientId: {ClientId}",
            string.IsNullOrWhiteSpace(clientId) ? "NOT SET" : "***configured***");
        _logger.LogInformation("SharePoint Certificate Thumbprint: {Thumbprint}",
            string.IsNullOrWhiteSpace(thumbprint) ? "NOT SET" : "***configured***");

        if (string.IsNullOrWhiteSpace(tenantId) ||
            string.IsNullOrWhiteSpace(clientId) ||
            string.IsNullOrWhiteSpace(thumbprint))
        {
            _logger.LogWarning("SharePoint settings for environment '{Environment}' are not fully configured. " +
                "SharePoint operations may fail.", environment);
        }

        // Validate Processing settings
        var processingSettings = new Core.Configuration.ProcessingSettings();
        _configuration.GetSection("Processing").Bind(processingSettings);
        _logger.LogInformation("Processing Settings - DefaultDelay: {DefaultDelay}ms, MinDelay: {MinDelay}ms, " +
            "MaxDelay: {MaxDelay}ms, MaxRetries: {MaxRetries}, BatchSize: {BatchSize}",
            processingSettings.DefaultDelay,
            processingSettings.MinDelay,
            processingSettings.MaxDelay,
            processingSettings.MaxRetries,
            processingSettings.BatchSize);

        _logger.LogInformation("Configuration validation completed successfully");
        return true;
    }

    /// <summary>
    /// Validates that RabbitMQ is installed and accessible
    /// </summary>
    private async Task<bool> ValidateRabbitMqInstallationAsync()
    {
        _logger.LogInformation("--- Validating RabbitMQ Installation ---");

        var host = _configuration["RabbitMQ:Host"] ?? "localhost";
        var port = int.Parse(_configuration["RabbitMQ:Port"] ?? "5672");

        // Check if RabbitMQ service is running on Windows
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "sc",
                    Arguments = "query RabbitMQ",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    var output = await process.StandardOutput.ReadToEndAsync();

                    if (process.ExitCode == 0)
                    {
                        if (output.Contains("RUNNING"))
                        {
                            _logger.LogInformation("RabbitMQ Windows service is RUNNING");
                        }
                        else if (output.Contains("STOPPED"))
                        {
                            _logger.LogError("RabbitMQ Windows service is installed but STOPPED");
                            _logger.LogError("Please start the RabbitMQ service using: net start RabbitMQ");
                            return false;
                        }
                        else
                        {
                            _logger.LogWarning("RabbitMQ service status: {Status}", output.Trim());
                        }
                    }
                    else
                    {
                        _logger.LogWarning("RabbitMQ Windows service not found. This may be expected on non-Windows systems.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not check RabbitMQ service status. Continuing with connection test.");
            }
        }

        // Test TCP connection to RabbitMQ port
        _logger.LogInformation("Testing connection to RabbitMQ at {Host}:{Port}", host, port);

        try
        {
            using var tcpClient = new TcpClient();
            var connectTask = tcpClient.ConnectAsync(host, port);
            var timeoutTask = Task.Delay(5000);

            var completedTask = await Task.WhenAny(connectTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _logger.LogError("Connection to RabbitMQ at {Host}:{Port} timed out after 5 seconds", host, port);
                _logger.LogError("RabbitMQ may not be installed or not running on the specified host/port");
                return false;
            }

            if (tcpClient.Connected)
            {
                _logger.LogInformation("Successfully connected to RabbitMQ port {Port}", port);
            }
            else
            {
                _logger.LogError("Failed to connect to RabbitMQ at {Host}:{Port}", host, port);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to RabbitMQ at {Host}:{Port}", host, port);
            _logger.LogError("Please ensure RabbitMQ is installed and running");
            return false;
        }

        // Test RabbitMQ authentication
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = host,
                Port = port,
                UserName = _configuration["RabbitMQ:Username"] ?? "guest",
                Password = _configuration["RabbitMQ:Password"] ?? "guest",
                VirtualHost = _configuration["RabbitMQ:VirtualHost"] ?? "/",
                RequestedConnectionTimeout = TimeSpan.FromSeconds(10)
            };

            _logger.LogInformation("Testing RabbitMQ authentication...");

            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            _logger.LogInformation("RabbitMQ authentication successful");
            _logger.LogInformation("RabbitMQ validation completed successfully");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to authenticate with RabbitMQ");
            _logger.LogError("Please check RabbitMQ credentials in configuration");
            return false;
        }
    }

    /// <summary>
    /// Validates database connection and checks for required tables
    /// </summary>
    private async Task<bool> ValidateDatabaseAsync()
    {
        _logger.LogInformation("--- Validating Database Connection and Schema ---");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ScyneShareContext>();

            // Test database connection
            _logger.LogInformation("Testing database connection...");
            var canConnect = await dbContext.Database.CanConnectAsync();

            if (!canConnect)
            {
                _logger.LogError("Cannot connect to database");
                _logger.LogError("Please check the connection string and ensure SQL Server is running");
                return false;
            }

            _logger.LogInformation("Database connection successful");

            // Check for pending migrations
            _logger.LogInformation("Checking for pending database migrations...");
            var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
            var pendingMigrationsList = pendingMigrations.ToList();

            if (pendingMigrationsList.Any())
            {
                _logger.LogWarning("Found {Count} pending migration(s):", pendingMigrationsList.Count);
                foreach (var migration in pendingMigrationsList)
                {
                    _logger.LogWarning("  - {Migration}", migration);
                }

                _logger.LogInformation("Attempting to apply pending migrations...");

                try
                {
                    await dbContext.Database.MigrateAsync();
                    _logger.LogInformation("Successfully applied all pending migrations");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to apply database migrations automatically");
                    _logger.LogError("Please apply migrations manually using: dotnet ef database update");
                    return false;
                }
            }
            else
            {
                _logger.LogInformation("No pending migrations found. Database schema is up to date.");
            }

            // Verify required tables exist
            _logger.LogInformation("Verifying required tables exist...");

            var requiredTables = new[]
            {
                ("ProcessingJobs", "Queue management"),
                ("ProcessingJobItems", "Queue items"),
                ("ProcessingJobLogs", "Processing logs"),
                ("Engagement", "Business data (read-only)"),
                ("Project", "Business data (read-only)"),
                ("Interaction", "Business data (read-only)"),
                ("InteractionMembership", "Business data (read-only)")
            };

            var allTablesExist = true;

            // Use a single connection for all table checks
            using var connection = dbContext.Database.GetDbConnection();
            await connection.OpenAsync();

            foreach (var (tableName, description) in requiredTables)
            {
                try
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = $"SELECT CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'ScyneShare' AND TABLE_NAME = '{tableName}') THEN 1 ELSE 0 END";

                    var result = await command.ExecuteScalarAsync();
                    var exists = Convert.ToInt32(result) == 1;

                    if (exists)
                    {
                        _logger.LogInformation("  ✓ Table '{TableName}' exists ({Description})", tableName, description);
                    }
                    else
                    {
                        _logger.LogError("  ✗ Table '{TableName}' does NOT exist ({Description})", tableName, description);
                        allTablesExist = false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not verify table '{TableName}'", tableName);
                }
            }

            if (!allTablesExist)
            {
                _logger.LogError("Some required database tables are missing");
                _logger.LogError("Please run database setup scripts or apply migrations");
                return false;
            }

            _logger.LogInformation("Database validation completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database validation failed");
            return false;
        }
    }

    /// <summary>
    /// Initializes RabbitMQ queues with proper configuration
    /// </summary>
    private async Task<bool> InitializeRabbitMqQueuesAsync()
    {
        _logger.LogInformation("--- Initializing RabbitMQ Queues ---");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var rabbitMqService = scope.ServiceProvider.GetRequiredService<RabbitMqService>();

            _logger.LogInformation("Initializing RabbitMQ connection and declaring queues...");
            await rabbitMqService.InitializeAsync();

            _logger.LogInformation("RabbitMQ queues initialized successfully");

            // List configured queues
            var queues = new[]
            {
                _configuration["RabbitMQ:Queues:InteractionPermissions"],
                _configuration["RabbitMQ:Queues:InteractionCreation"],
                _configuration["RabbitMQ:Queues:RemovePermissions"],
                _configuration["RabbitMQ:Queues:DeadLetter"]
            };

            _logger.LogInformation("Configured queues:");
            foreach (var queue in queues.Where(q => !string.IsNullOrWhiteSpace(q)))
            {
                _logger.LogInformation("  - {QueueName}", queue);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize RabbitMQ queues");
            return false;
        }
    }

    /// <summary>
    /// Sanitizes connection string to hide sensitive information
    /// </summary>
    private static string SanitizeConnectionString(string connectionString)
    {
        var parts = connectionString.Split(';');
        var sanitized = new List<string>();

        foreach (var part in parts)
        {
            if (part.Trim().StartsWith("Password", StringComparison.OrdinalIgnoreCase) ||
                part.Trim().StartsWith("Pwd", StringComparison.OrdinalIgnoreCase))
            {
                sanitized.Add("Password=***");
            }
            else
            {
                sanitized.Add(part);
            }
        }

        return string.Join(";", sanitized);
    }
}
