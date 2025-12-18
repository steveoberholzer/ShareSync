using Microsoft.EntityFrameworkCore;
using Serilog;
using SharePointPermissionSync.Data;
using SharePointPermissionSync.Data.Repositories;
using SharePointPermissionSync.Data.Services;
using SharePointPermissionSync.Web.Hubs;
using SharePointPermissionSync.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: "logs/web-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllersWithViews();

// Configure DbContext
builder.Services.AddDbContext<ScyneShareContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("ScyneShareDb"),
        sqlOptions => sqlOptions.CommandTimeout(120)));

// Register repositories
builder.Services.AddScoped<IJobRepository, JobRepository>();
builder.Services.AddScoped<ILogRepository, LogRepository>();

// Register application services
builder.Services.AddSingleton<QueueService>();
builder.Services.AddScoped<JobService>();
builder.Services.AddScoped<LogService>();

// Add SignalR
builder.Services.AddSignalR();

// Add session support (optional, for user tracking)
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();
app.UseSession();

// Map controllers
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Jobs}/{action=Index}/{id?}");

// Map SignalR hub
app.MapHub<JobProgressHub>("/hubs/jobprogress");

Log.Information("SharePoint Permission Sync Web Portal starting...");

try
{
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
