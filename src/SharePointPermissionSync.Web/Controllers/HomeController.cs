using Microsoft.AspNetCore.Mvc;
using SharePointPermissionSync.Web.Services;

namespace SharePointPermissionSync.Web.Controllers;

public class HomeController : Controller
{
    private readonly JobService _jobService;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        JobService jobService,
        ILogger<HomeController> logger)
    {
        _jobService = jobService;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            // Get dashboard statistics
            var allJobs = await _jobService.GetJobsAsync(null, 0, 1000);

            ViewBag.TotalJobs = allJobs.Count;
            ViewBag.ActiveJobs = allJobs.Count(j => j.Status == "Processing" || j.Status == "Queued");
            ViewBag.CompletedJobs = allJobs.Count(j => j.Status == "Completed");
            ViewBag.FailedJobs = allJobs.Count(j => j.Status == "Failed" || j.Status == "Completed with Errors");

            // Calculate success rate
            var completedCount = allJobs.Count(j => j.Status == "Completed" || j.Status == "Failed" || j.Status == "Completed with Errors");
            var successCount = allJobs.Count(j => j.Status == "Completed");
            ViewBag.SuccessRate = completedCount > 0 ? (int)((double)successCount / completedCount * 100) : 0;

            // Recent jobs (last 10)
            ViewBag.RecentJobs = allJobs.OrderByDescending(j => j.CreatedAt).Take(10).ToList();

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dashboard");
            ViewBag.TotalJobs = 0;
            ViewBag.ActiveJobs = 0;
            ViewBag.CompletedJobs = 0;
            ViewBag.FailedJobs = 0;
            ViewBag.SuccessRate = 0;
            ViewBag.RecentJobs = new List<SharePointPermissionSync.Data.Entities.ProcessingJob>();
            return View();
        }
    }
}
