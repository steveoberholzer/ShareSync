using Microsoft.AspNetCore.Mvc;
using SharePointPermissionSync.Web.Services;

namespace SharePointPermissionSync.Web.Controllers;

public class JobsController : Controller
{
    private readonly JobService _jobService;
    private readonly ILogger<JobsController> _logger;

    public JobsController(
        JobService jobService,
        ILogger<JobsController> logger)
    {
        _jobService = jobService;
        _logger = logger;
    }

    /// <summary>
    /// List all jobs
    /// </summary>
    public async Task<IActionResult> Index(string? status, int page = 1, int pageSize = 20)
    {
        var skip = (page - 1) * pageSize;
        var jobs = await _jobService.GetJobsAsync(status, skip, pageSize);

        ViewBag.CurrentStatus = status;
        ViewBag.CurrentPage = page;
        ViewBag.PageSize = pageSize;

        return View(jobs);
    }

    /// <summary>
    /// Job details with items
    /// </summary>
    public async Task<IActionResult> Details(Guid id)
    {
        var job = await _jobService.GetJobAsync(id);

        if (job == null)
            return NotFound();

        var items = await _jobService.GetJobItemsAsync(id);

        ViewBag.Items = items;

        return View(job);
    }

    /// <summary>
    /// Get job progress (for AJAX/SignalR)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Progress(Guid id)
    {
        var job = await _jobService.GetJobAsync(id);

        if (job == null)
            return NotFound();

        return Json(new
        {
            jobId = job.JobId,
            status = job.Status,
            totalItems = job.TotalItems,
            processedItems = job.ProcessedItems,
            failedItems = job.FailedItems,
            progressPercentage = job.TotalItems > 0
                ? Math.Round((decimal)job.ProcessedItems / job.TotalItems * 100, 2)
                : 0,
            startedAt = job.StartedAt,
            completedAt = job.CompletedAt,
            errorMessage = job.ErrorMessage
        });
    }

    /// <summary>
    /// Cancel a job and all its pending items
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id)
    {
        try
        {
            await _jobService.CancelJobAsync(id);
            _logger.LogInformation("Job {JobId} cancelled", id);
            TempData["SuccessMessage"] = "Job cancelled successfully. All pending items have been cancelled.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling job {JobId}", id);
            TempData["ErrorMessage"] = $"Failed to cancel job: {ex.Message}";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Restart a job by creating a new job with all items
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restart(Guid id)
    {
        try
        {
            var newJobId = await _jobService.RestartJobAsync(id);
            _logger.LogInformation("Job {OldJobId} restarted as new job {NewJobId}", id, newJobId);
            TempData["SuccessMessage"] = $"Job restarted successfully as new job. Click here to view the new job.";

            // Redirect to the new job
            return RedirectToAction(nameof(Details), new { id = newJobId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restarting job {JobId}", id);
            TempData["ErrorMessage"] = $"Failed to restart job: {ex.Message}";
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// Update job priority
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePriority(Guid id, string priority)
    {
        try
        {
            await _jobService.UpdateJobPriorityAsync(id, priority);
            _logger.LogInformation("Job {JobId} priority updated to {Priority}", id, priority);
            TempData["SuccessMessage"] = $"Job priority updated to {priority}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating priority for job {JobId}", id);
            TempData["ErrorMessage"] = $"Failed to update priority: {ex.Message}";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Update job item payload
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateItemPayload(Guid id, Guid messageId, string payload)
    {
        try
        {
            await _jobService.UpdateJobItemPayloadAsync(messageId, payload);
            _logger.LogInformation("Job item {MessageId} payload updated", messageId);
            TempData["SuccessMessage"] = "Item payload updated successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating payload for item {MessageId}", messageId);
            TempData["ErrorMessage"] = $"Failed to update payload: {ex.Message}";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Search job items across all jobs
    /// </summary>
    public async Task<IActionResult> Search(
        string? searchText,
        DateTime? fromDate,
        DateTime? toDate,
        string? status,
        string? itemType,
        int page = 1,
        int pageSize = 50)
    {
        // Default to last 7 days if no date range specified
        if (!fromDate.HasValue && !toDate.HasValue)
        {
            fromDate = DateTime.UtcNow.AddDays(-7);
            toDate = DateTime.UtcNow;
        }

        var skip = (page - 1) * pageSize;
        var items = await _jobService.SearchJobItemsAsync(searchText, fromDate, toDate, status, itemType, skip, pageSize);
        var totalCount = await _jobService.SearchJobItemsCountAsync(searchText, fromDate, toDate, status, itemType);

        ViewBag.SearchText = searchText;
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;
        ViewBag.Status = status;
        ViewBag.ItemType = itemType;
        ViewBag.CurrentPage = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        return View(items);
    }
}
