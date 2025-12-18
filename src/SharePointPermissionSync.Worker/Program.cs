using Microsoft.EntityFrameworkCore;
using SharePointPermissionSync.Core.Configuration;
using SharePointPermissionSync.Data;
using SharePointPermissionSync.Data.Repositories;
using SharePointPermissionSync.Data.Services;
using SharePointPermissionSync.Worker.Handlers;
using SharePointPermissionSync.Worker.Services;
using SharePointPermissionSync.Worker.Workers;
using Serilog;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/worker-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Starting SharePoint Permission Sync Worker Service");

    var builder = Host.CreateApplicationBuilder(args);

    // Add Serilog
    builder.Services.AddSerilog();

    // Configure settings
    var processingSettings = new ProcessingSettings();
    builder.Configuration.GetSection("Processing").Bind(processingSettings);
    builder.Services.AddSingleton(processingSettings);

    // Add DbContext
    builder.Services.AddDbContext<ScyneShareContext>(options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            sqlOptions => sqlOptions.EnableRetryOnFailure()));

    // Add Repositories
    builder.Services.AddScoped<IJobRepository, JobRepository>();
    builder.Services.AddScoped<ILogRepository, LogRepository>();
    builder.Services.AddScoped<IInteractionRepository, InteractionRepository>();

    // Add Services
    builder.Services.AddSingleton<ThrottleManager>();
    builder.Services.AddScoped<SharePointOperationService>();
    builder.Services.AddScoped<RabbitMqService>();
    builder.Services.AddScoped<QueueConsumer>();
    builder.Services.AddScoped<MessageProcessor>();
    builder.Services.AddScoped<LogService>();

    // Add Handlers
    builder.Services.AddScoped<InteractionPermissionHandler>();
    builder.Services.AddScoped<InteractionCreationHandler>();
    builder.Services.AddScoped<RemoveUniquePermissionHandler>();

    // Add Background Worker
    builder.Services.AddHostedService<QueueProcessorWorker>();

    var host = builder.Build();

    Log.Information("Worker Service configured successfully");

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Worker Service terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
