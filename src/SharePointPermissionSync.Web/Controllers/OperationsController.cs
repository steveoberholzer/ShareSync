using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Mvc;
using SharePointPermissionSync.Core.Models.Messages;
using SharePointPermissionSync.Core.Utilities;
using SharePointPermissionSync.Data.Repositories;
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
                HasHeaderRecord = true,
                MissingFieldFound = null, // Ignore missing fields
                HeaderValidated = null    // Don't validate headers
            });

            var records = csv.GetRecords<InteractionPermissionCsvRow>().ToList();

            // Inject repository for database lookups
            var interactionRepo = HttpContext.RequestServices.GetRequiredService<IInteractionRepository>();

            var messages = new List<InteractionPermissionMessage>();

            foreach (var row in records)
            {
                // Query database to get entity details and SharePointFolderIDs
                var interaction = await interactionRepo.GetByIdAsync(row.InteractionId);
                if (interaction == null)
                {
                    _logger.LogWarning("Interaction {InteractionId} not found, skipping", row.InteractionId);
                    continue;
                }

                var project = await interactionRepo.GetProjectByIdAsync(row.ProjectId);
                if (project == null)
                {
                    _logger.LogWarning("Project {ProjectId} not found, skipping", row.ProjectId);
                    continue;
                }

                var engagement = await interactionRepo.GetEngagementByIdAsync(row.EngagementId);
                if (engagement == null)
                {
                    _logger.LogWarning("Engagement {EngagementId} not found, skipping", row.EngagementId);
                    continue;
                }

                // Validate that all SharePoint folder IDs exist
                if (!interaction.SharePointFolderID.HasValue || interaction.SharePointFolderID.Value <= 0)
                {
                    _logger.LogWarning(
                        "Interaction {InteractionId} has no SharePoint folder ID, skipping",
                        row.InteractionId);
                    continue;
                }

                if (!project.SharePointFolderID.HasValue || project.SharePointFolderID.Value <= 0)
                {
                    _logger.LogWarning(
                        "Project {ProjectId} has no SharePoint folder ID, skipping",
                        row.ProjectId);
                    continue;
                }

                if (!engagement.SharePointFolderID.HasValue || engagement.SharePointFolderID.Value <= 0)
                {
                    _logger.LogWarning(
                        "Engagement {EngagementId} has no SharePoint folder ID, skipping",
                        row.EngagementId);
                    continue;
                }

                // Create message with full hierarchy information
                var message = new InteractionPermissionMessage
                {
                    MessageId = Guid.NewGuid(),
                    OperationType = "InteractionPermissionSync",

                    // GUID identifiers
                    InteractionId = row.InteractionId,
                    ProjectId = row.ProjectId,
                    EngagementId = row.EngagementId,

                    // SharePoint folder IDs (non-nullable - validated above)
                    InteractionSharePointFolderId = interaction.SharePointFolderID.Value,
                    ProjectSharePointFolderId = project.SharePointFolderID.Value,
                    EngagementSharePointFolderId = engagement.SharePointFolderID.Value,

                    // Business identifiers (cleaned)
                    InteractionName = SharePointNameHelper.FormatInteractionName(
                        interaction.InteractionNumber,
                        interaction.Name),
                    ProjectName = SharePointNameHelper.CleanFolderName(project.Name),
                    EngagementName = SharePointNameHelper.CleanFolderName(engagement.Name),

                    // SharePoint configuration
                    SiteUrl = string.IsNullOrWhiteSpace(row.SiteUrl) ? siteUrl : row.SiteUrl,
                    DocumentLibrary = "Documents",

                    // Permissions
                    InternalPermission = row.InternalPermission ?? "Read",
                    InternalUserEmails = row.InternalUserEmails?
                        .Split(';', StringSplitOptions.RemoveEmptyEntries)
                        .ToList() ?? new(),
                    ExternalPermission = row.ExternalPermission ?? string.Empty,
                    ExternalUserEmails = row.ExternalUserEmails?
                        .Split(';', StringSplitOptions.RemoveEmptyEntries)
                        .ToList() ?? new()
                };

                messages.Add(message);
            }

            if (messages.Count == 0)
            {
                ModelState.AddModelError("", "No valid records found in CSV file");
                return View("InteractionPermissions");
            }

            var jobId = await _jobService.CreateJobAsync(
                "InteractionPermissionSync",
                csvFile.FileName,
                User.Identity?.Name ?? "Anonymous",
                environment,
                siteUrl,
                messages,
                priority);

            _logger.LogInformation(
                "Created InteractionPermissionSync job {JobId} with {Count} items (from {TotalRows} rows)",
                jobId,
                messages.Count,
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
                HasHeaderRecord = true,
                MissingFieldFound = null, // Ignore missing fields
                HeaderValidated = null    // Don't validate headers
            });

            var records = csv.GetRecords<InteractionCreationCsvRow>().ToList();

            // Inject repository for database lookups
            var interactionRepo = HttpContext.RequestServices.GetRequiredService<IInteractionRepository>();

            var messages = new List<InteractionCreationMessage>();

            foreach (var row in records)
            {
                // Query database to get entity details
                var interaction = await interactionRepo.GetByIdAsync(row.InteractionId);
                if (interaction == null)
                {
                    _logger.LogWarning("Interaction {InteractionId} not found, skipping", row.InteractionId);
                    continue;
                }

                var project = await interactionRepo.GetProjectByIdAsync(row.ProjectId);
                if (project == null)
                {
                    _logger.LogWarning("Project {ProjectId} not found, skipping", row.ProjectId);
                    continue;
                }

                var engagement = await interactionRepo.GetEngagementByIdAsync(row.EngagementId);
                if (engagement == null)
                {
                    _logger.LogWarning("Engagement {EngagementId} not found, skipping", row.EngagementId);
                    continue;
                }

                // Create message with full hierarchy
                var message = new InteractionCreationMessage
                {
                    MessageId = Guid.NewGuid(),
                    OperationType = "InteractionCreation",

                    // GUID identifiers
                    InteractionId = row.InteractionId,
                    ProjectId = row.ProjectId,
                    EngagementId = row.EngagementId,

                    // SharePoint folder IDs (nullable - may need creation)
                    // Engagement folder should already exist (per requirements)
                    EngagementSharePointFolderId = engagement.SharePointFolderID,
                    ProjectSharePointFolderId = project.SharePointFolderID,
                    InteractionSharePointFolderId = interaction.SharePointFolderID, // Should be null for creation

                    // Business identifiers (cleaned)
                    InteractionName = SharePointNameHelper.FormatInteractionName(
                        interaction.InteractionNumber,
                        interaction.Name),
                    ProjectName = SharePointNameHelper.CleanFolderName(project.Name),
                    EngagementName = SharePointNameHelper.CleanFolderName(engagement.Name),

                    // SharePoint configuration
                    SiteUrl = string.IsNullOrWhiteSpace(row.SiteUrl) ? siteUrl : row.SiteUrl,
                    DocumentLibrary = "Documents",
                    ProjectSubfolder = row.ProjectSubfolder ?? string.Empty,
                    CreatedBy = User.Identity?.Name ?? "Anonymous",

                    // Permissions
                    InternalPermission = row.InternalPermission ?? "Read",
                    InternalUserEmails = row.InternalUserEmails?
                        .Split(';', StringSplitOptions.RemoveEmptyEntries)
                        .ToList() ?? new(),
                    ExternalPermission = row.ExternalPermission ?? string.Empty,
                    ExternalUserEmails = row.ExternalUserEmails?
                        .Split(';', StringSplitOptions.RemoveEmptyEntries)
                        .ToList() ?? new()
                };

                messages.Add(message);
            }

            if (messages.Count == 0)
            {
                ModelState.AddModelError("", "No valid records found in CSV file");
                return View("InteractionCreation");
            }

            // IMPORTANT: Sort messages so Projects are created before their Interactions
            var sortedMessages = messages
                .OrderBy(m => m.ProjectId)
                .ThenBy(m => m.InteractionName)
                .ToList();

            var jobId = await _jobService.CreateJobAsync(
                "InteractionCreation",
                csvFile.FileName,
                User.Identity?.Name ?? "Anonymous",
                environment,
                siteUrl,
                sortedMessages,
                priority);

            _logger.LogInformation(
                "Created InteractionCreation job {JobId} with {Count} items (from {TotalRows} rows)",
                jobId,
                messages.Count,
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
    public Guid InteractionId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid EngagementId { get; set; }
    public string? SiteUrl { get; set; }
    // SharePointFolderId removed - queried from database
    public string? InternalPermission { get; set; }
    public string? InternalUserEmails { get; set; }
    public string? ExternalPermission { get; set; }
    public string? ExternalUserEmails { get; set; }
}

public class InteractionCreationCsvRow
{
    public Guid InteractionId { get; set; } // Added - needed for database update after creation
    public Guid ProjectId { get; set; }
    public Guid EngagementId { get; set; }
    public string? SiteUrl { get; set; }
    public string? ProjectSubfolder { get; set; }
    public string? InternalPermission { get; set; }
    public string? InternalUserEmails { get; set; }
    public string? ExternalPermission { get; set; }
    public string? ExternalUserEmails { get; set; }
}
