using Microsoft.AspNetCore.Mvc;
using SharePointPermissionSync.Data.Repositories;
using SharePointPermissionSync.Web.Services;
using System.Text.Json;

namespace SharePointPermissionSync.Web.Controllers;

/// <summary>
/// Controller for queue management operations
/// </summary>
public class QueueController : Controller
{
    private readonly IJobRepository _jobRepository;
    private readonly QueueService _queueService;
    private readonly ILogger<QueueController> _logger;

    public QueueController(
        IJobRepository jobRepository,
        QueueService queueService,
        ILogger<QueueController> logger)
    {
        _jobRepository = jobRepository;
        _queueService = queueService;
        _logger = logger;
    }

    /// <summary>
    /// Display all queue items across all jobs
    /// </summary>
    public async Task<IActionResult> Items(
        string? status = null,
        string? itemType = null,
        string? search = null,
        int page = 1,
        int pageSize = 50)
    {
        var skip = (page - 1) * pageSize;

        var items = await _jobRepository.GetAllJobItemsAsync(
            status,
            itemType,
            search,
            skip,
            pageSize);

        var totalCount = await _jobRepository.GetAllJobItemsCountAsync(
            status,
            itemType,
            search);

        ViewBag.CurrentStatus = status;
        ViewBag.CurrentItemType = itemType;
        ViewBag.CurrentSearch = search;
        ViewBag.CurrentPage = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return View(items);
    }

    /// <summary>
    /// Get item details including payload
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ItemDetails(Guid messageId)
    {
        var item = await _jobRepository.GetJobItemByMessageIdAsync(messageId);

        if (item == null)
            return NotFound();

        // Format the payload JSON for display
        string formattedPayload = "{}";
        if (!string.IsNullOrEmpty(item.Payload))
        {
            try
            {
                var jsonDocument = JsonDocument.Parse(item.Payload);
                formattedPayload = JsonSerializer.Serialize(
                    jsonDocument,
                    new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                formattedPayload = item.Payload;
            }
        }

        return Json(new
        {
            messageId = item.MessageId,
            jobId = item.JobId,
            itemType = item.ItemType,
            itemIdentifier = item.ItemIdentifier,
            status = item.Status,
            retryCount = item.RetryCount,
            maxRetries = item.MaxRetries,
            errorMessage = item.ErrorMessage,
            payload = formattedPayload,
            createdAt = item.CreatedAt,
            processedAt = item.ProcessedAt
        });
    }

    /// <summary>
    /// Retry a failed item by republishing to queue
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> RetryItem(Guid messageId)
    {
        try
        {
            var item = await _jobRepository.GetJobItemByMessageIdAsync(messageId);

            if (item == null)
                return NotFound(new { success = false, message = "Item not found" });

            // Only allow retry for failed items
            if (item.Status != "Failed")
                return BadRequest(new { success = false, message = "Only failed items can be retried" });

            if (string.IsNullOrEmpty(item.Payload))
                return BadRequest(new { success = false, message = "Item has no payload to retry" });

            // Reset the item status to pending
            await _jobRepository.UpdateJobItemStatusAsync(
                messageId,
                "Pending",
                errorMessage: null,
                retryCount: 0);

            // Republish to queue
            var queueName = GetQueueNameForItemType(item.ItemType ?? "");
            await _queueService.PublishMessageAsync(queueName, item.Payload);

            _logger.LogInformation(
                "Item {MessageId} manually retried and republished to queue {QueueName}",
                messageId,
                queueName);

            return Json(new { success = true, message = "Item successfully retried and added back to queue" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrying item {MessageId}", messageId);
            return StatusCode(500, new { success = false, message = $"Error: {ex.Message}" });
        }
    }

    /// <summary>
    /// Delete an item from the database
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> DeleteItem(Guid messageId)
    {
        try
        {
            var item = await _jobRepository.GetJobItemByMessageIdAsync(messageId);

            if (item == null)
                return NotFound(new { success = false, message = "Item not found" });

            // Only allow deletion of completed or failed items
            if (item.Status != "Completed" && item.Status != "Failed")
                return BadRequest(new
                {
                    success = false,
                    message = "Only completed or failed items can be deleted. Pending/Processing items should be allowed to complete."
                });

            var deleted = await _jobRepository.DeleteJobItemAsync(messageId);

            if (deleted)
            {
                _logger.LogInformation("Item {MessageId} deleted from database", messageId);
                return Json(new { success = true, message = "Item successfully deleted" });
            }

            return StatusCode(500, new { success = false, message = "Failed to delete item" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting item {MessageId}", messageId);
            return StatusCode(500, new { success = false, message = $"Error: {ex.Message}" });
        }
    }

    /// <summary>
    /// Get statistics for the queue dashboard
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Statistics()
    {
        try
        {
            var pendingCount = await _jobRepository.GetAllJobItemsCountAsync(status: "Pending");
            var processingCount = await _jobRepository.GetAllJobItemsCountAsync(status: "Processing");
            var completedCount = await _jobRepository.GetAllJobItemsCountAsync(status: "Completed");
            var failedCount = await _jobRepository.GetAllJobItemsCountAsync(status: "Failed");
            var totalCount = await _jobRepository.GetAllJobItemsCountAsync();

            return Json(new
            {
                pending = pendingCount,
                processing = processingCount,
                completed = completedCount,
                failed = failedCount,
                total = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting queue statistics");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private string GetQueueNameForItemType(string itemType)
    {
        return itemType switch
        {
            "InteractionPermissionSync" => "sharepoint.interaction.permissions",
            "InteractionCreation" => "sharepoint.interaction.creation",
            "RemoveUniquePermissions" => "sharepoint.remove.permissions",
            _ => "sharepoint.interaction.permissions" // Default fallback
        };
    }
}
