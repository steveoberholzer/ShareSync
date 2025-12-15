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
}
