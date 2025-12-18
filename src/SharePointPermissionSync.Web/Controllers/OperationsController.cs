using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Mvc;
using SharePointPermissionSync.Core.Models.Messages;
using SharePointPermissionSync.Web.Services;

namespace SharePointPermissionSync.Web.Controllers;

public class OperationsController : Controller
{
    private readonly JobService _jobService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OperationsController> _logger;

    public OperationsController(
        JobService jobService,
        IConfiguration configuration,
        ILogger<OperationsController> logger)
    {
        _jobService = jobService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Interaction permission sync form
    /// </summary>
    public IActionResult InteractionPermissions()
    {
        return View();
    }

    /// <summary>
    /// Upload CSV for interaction permissions
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadInteractionPermissions(
        IFormFile csvFile,
        string environment,
        string siteUrl,
        string priority = "Medium")
    {
        if (csvFile == null || csvFile.Length == 0)
        {
            ModelState.AddModelError("", "Please select a CSV file");
            return View("InteractionPermissions");
        }

        try
        {
            using var reader = new StreamReader(csvFile.OpenReadStream());
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true
            });

            var records = csv.GetRecords<InteractionPermissionCsvRow>().ToList();

            var messages = records.Select(row => new InteractionPermissionMessage
            {
                MessageId = Guid.NewGuid(),
                OperationType = "InteractionPermissionSync",
                InteractionId = row.InteractionId,
                ProjectId = row.ProjectId,
                EngagementId = row.EngagementId,
                SiteUrl = string.IsNullOrWhiteSpace(row.SiteUrl) ? siteUrl : row.SiteUrl,
                DocumentLibrary = "Documents",
                SharePointFolderId = row.SharePointFolderId,
                InternalPermission = row.InternalPermission ?? "Read",
                InternalUserEmails = row.InternalUserEmails?.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new(),
                ExternalPermission = row.ExternalPermission ?? string.Empty,
                ExternalUserEmails = row.ExternalUserEmails?.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new()
            });

            var jobId = await _jobService.CreateJobAsync(
                "InteractionPermissionSync",
                csvFile.FileName,
                User.Identity?.Name ?? "Anonymous",
                environment,
                siteUrl,
                messages,
                priority);

            _logger.LogInformation(
                "Created InteractionPermissionSync job {JobId} with {Count} items",
                jobId,
                records.Count);

            return RedirectToAction("Details", "Jobs", new { id = jobId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading CSV file");
            ModelState.AddModelError("", $"Error processing CSV: {ex.Message}");
            return View("InteractionPermissions");
        }
    }

    /// <summary>
    /// Interaction creation form
    /// </summary>
    public IActionResult InteractionCreation()
    {
        return View();
    }

    /// <summary>
    /// Upload CSV for interaction creation
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadInteractionCreation(
        IFormFile csvFile,
        string environment,
        string siteUrl,
        string priority = "Medium")
    {
        if (csvFile == null || csvFile.Length == 0)
        {
            ModelState.AddModelError("", "Please select a CSV file");
            return View("InteractionCreation");
        }

        try
        {
            using var reader = new StreamReader(csvFile.OpenReadStream());
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true
            });

            var records = csv.GetRecords<InteractionCreationCsvRow>().ToList();

            var messages = records.Select(row => new InteractionCreationMessage
            {
                MessageId = Guid.NewGuid(),
                OperationType = "InteractionCreation",
                InteractionName = row.InteractionName,
                ProjectId = row.ProjectId,
                EngagementId = row.EngagementId,
                SiteUrl = string.IsNullOrWhiteSpace(row.SiteUrl) ? siteUrl : row.SiteUrl,
                DocumentLibrary = "Documents",
                ProjectSubfolder = row.ProjectSubfolder ?? string.Empty,
                CreatedBy = User.Identity?.Name ?? "Anonymous",
                InternalPermission = row.InternalPermission ?? "Read",
                InternalUserEmails = row.InternalUserEmails?.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new(),
                ExternalPermission = row.ExternalPermission ?? string.Empty,
                ExternalUserEmails = row.ExternalUserEmails?.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new()
            });

            var jobId = await _jobService.CreateJobAsync(
                "InteractionCreation",
                csvFile.FileName,
                User.Identity?.Name ?? "Anonymous",
                environment,
                siteUrl,
                messages,
                priority);

            _logger.LogInformation(
                "Created InteractionCreation job {JobId} with {Count} items",
                jobId,
                records.Count);

            return RedirectToAction("Details", "Jobs", new { id = jobId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading CSV file");
            ModelState.AddModelError("", $"Error processing CSV: {ex.Message}");
            return View("InteractionCreation");
        }
    }
}

// CSV row models
public class InteractionPermissionCsvRow
{
    public int InteractionId { get; set; }
    public int ProjectId { get; set; }
    public int EngagementId { get; set; }
    public string? SiteUrl { get; set; }
    public int SharePointFolderId { get; set; }
    public string? InternalPermission { get; set; }
    public string? InternalUserEmails { get; set; }
    public string? ExternalPermission { get; set; }
    public string? ExternalUserEmails { get; set; }
}

public class InteractionCreationCsvRow
{
    public string InteractionName { get; set; } = string.Empty;
    public int ProjectId { get; set; }
    public int EngagementId { get; set; }
    public string? SiteUrl { get; set; }
    public string? ProjectSubfolder { get; set; }
    public string? InternalPermission { get; set; }
    public string? InternalUserEmails { get; set; }
    public string? ExternalPermission { get; set; }
    public string? ExternalUserEmails { get; set; }
}
