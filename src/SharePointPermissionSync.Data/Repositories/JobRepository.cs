using Microsoft.EntityFrameworkCore;
using SharePointPermissionSync.Data.Entities;

namespace SharePointPermissionSync.Data.Repositories;

/// <summary>
/// Repository for managing processing jobs
/// </summary>
public class JobRepository : IJobRepository
{
    private readonly ScyneShareContext _context;

    public JobRepository(ScyneShareContext context)
    {
        _context = context;
    }

    public async Task<ProcessingJob> CreateJobAsync(ProcessingJob job)
    {
        _context.ProcessingJobs.Add(job);
        await _context.SaveChangesAsync();
        return job;
    }

    public async Task<ProcessingJob?> GetJobByIdAsync(Guid jobId)
    {
        return await _context.ProcessingJobs
            .Include(j => j.Items)
            .FirstOrDefaultAsync(j => j.JobId == jobId);
    }

    public async Task<string?> GetJobStatusAsync(Guid jobId)
    {
        // Force fresh fetch from database, no tracking, only get status
        return await _context.ProcessingJobs
            .AsNoTracking()
            .Where(j => j.JobId == jobId)
            .Select(j => j.Status)
            .FirstOrDefaultAsync();
    }

    public async Task<List<ProcessingJob>> GetJobsAsync(string? status = null, int skip = 0, int take = 50)
    {
        var query = _context.ProcessingJobs.AsQueryable();

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(j => j.Status == status);
        }

        return await query
            .OrderByDescending(j => j.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task UpdateJobStatusAsync(Guid jobId, string status, string? errorMessage = null)
    {
        var job = await _context.ProcessingJobs.FirstOrDefaultAsync(j => j.JobId == jobId);
        if (job != null)
        {
            job.Status = status;
            if (errorMessage != null)
            {
                job.ErrorMessage = errorMessage;
            }
            await _context.SaveChangesAsync();
        }
    }

    public async Task IncrementProcessedCountAsync(Guid jobId)
    {
        var job = await _context.ProcessingJobs.FirstOrDefaultAsync(j => j.JobId == jobId);
        if (job != null)
        {
            job.ProcessedItems++;
            await _context.SaveChangesAsync();
        }
    }

    public async Task IncrementFailedCountAsync(Guid jobId)
    {
        var job = await _context.ProcessingJobs.FirstOrDefaultAsync(j => j.JobId == jobId);
        if (job != null)
        {
            job.FailedItems++;
            await _context.SaveChangesAsync();
        }
    }

    public async Task MarkJobAsStartedAsync(Guid jobId)
    {
        var job = await _context.ProcessingJobs.FirstOrDefaultAsync(j => j.JobId == jobId);
        if (job != null)
        {
            job.StartedAt = DateTime.UtcNow;
            job.Status = "Processing";
            await _context.SaveChangesAsync();
        }
    }

    public async Task MarkJobAsCompletedAsync(Guid jobId)
    {
        var job = await _context.ProcessingJobs.FirstOrDefaultAsync(j => j.JobId == jobId);
        if (job != null)
        {
            job.CompletedAt = DateTime.UtcNow;
            job.Status = "Completed";
            await _context.SaveChangesAsync();
        }
    }

    public async Task<ProcessingJobItem> AddJobItemAsync(ProcessingJobItem item)
    {
        _context.ProcessingJobItems.Add(item);
        await _context.SaveChangesAsync();
        return item;
    }

    public async Task UpdateJobItemStatusAsync(Guid messageId, string status, string? errorMessage = null, int? retryCount = null)
    {
        var item = await _context.ProcessingJobItems.FirstOrDefaultAsync(i => i.MessageId == messageId);
        if (item != null)
        {
            item.Status = status;
            if (errorMessage != null)
            {
                item.ErrorMessage = errorMessage;
            }
            if (retryCount.HasValue)
            {
                item.RetryCount = retryCount.Value;
            }
            if (status == "Completed" || status == "Failed")
            {
                item.ProcessedAt = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<ProcessingJobItem>> GetJobItemsAsync(Guid jobId, string? status = null)
    {
        var query = _context.ProcessingJobItems.Where(i => i.JobId == jobId);

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(i => i.Status == status);
        }

        return await query
            .OrderBy(i => i.CreatedAt)
            .ToListAsync();
    }

    public async Task<ProcessingJobItem?> GetJobItemByMessageIdAsync(Guid messageId)
    {
        return await _context.ProcessingJobItems
            .FirstOrDefaultAsync(i => i.MessageId == messageId);
    }

    public async Task<List<ProcessingJobItem>> GetAllJobItemsAsync(
        string? status = null,
        string? itemType = null,
        string? searchTerm = null,
        int skip = 0,
        int take = 100)
    {
        var query = _context.ProcessingJobItems.AsQueryable();

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(i => i.Status == status);
        }

        if (!string.IsNullOrEmpty(itemType))
        {
            query = query.Where(i => i.ItemType == itemType);
        }

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(i =>
                i.ItemIdentifier != null && i.ItemIdentifier.Contains(searchTerm) ||
                i.ErrorMessage != null && i.ErrorMessage.Contains(searchTerm));
        }

        return await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<int> GetAllJobItemsCountAsync(
        string? status = null,
        string? itemType = null,
        string? searchTerm = null)
    {
        var query = _context.ProcessingJobItems.AsQueryable();

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(i => i.Status == status);
        }

        if (!string.IsNullOrEmpty(itemType))
        {
            query = query.Where(i => i.ItemType == itemType);
        }

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(i =>
                i.ItemIdentifier != null && i.ItemIdentifier.Contains(searchTerm) ||
                i.ErrorMessage != null && i.ErrorMessage.Contains(searchTerm));
        }

        return await query.CountAsync();
    }

    public async Task<bool> DeleteJobItemAsync(Guid messageId)
    {
        var item = await _context.ProcessingJobItems
            .FirstOrDefaultAsync(i => i.MessageId == messageId);

        if (item == null)
            return false;

        _context.ProcessingJobItems.Remove(item);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task UpdateJobPriorityAsync(Guid jobId, string priority)
    {
        var job = await _context.ProcessingJobs
            .FirstOrDefaultAsync(j => j.JobId == jobId);

        if (job != null)
        {
            job.Priority = priority;
            await _context.SaveChangesAsync();
        }
    }

    public async Task CancelJobAsync(Guid jobId)
    {
        var job = await _context.ProcessingJobs
            .FirstOrDefaultAsync(j => j.JobId == jobId);

        if (job != null)
        {
            job.Status = "Cancelled";
            await _context.SaveChangesAsync();

            // Cancel all Pending items (not Processing, Completed, or Failed)
            await BulkUpdateJobItemStatusAsync(jobId, "Pending", "Cancelled");
        }
    }

    public async Task BulkUpdateJobItemStatusAsync(Guid jobId, string fromStatus, string toStatus)
    {
        var items = await _context.ProcessingJobItems
            .Where(i => i.JobId == jobId && i.Status == fromStatus)
            .ToListAsync();

        foreach (var item in items)
        {
            item.Status = toStatus;
        }

        await _context.SaveChangesAsync();
    }

    public async Task UpdateJobItemPayloadAsync(Guid messageId, string payload)
    {
        var item = await _context.ProcessingJobItems
            .FirstOrDefaultAsync(i => i.MessageId == messageId);

        if (item != null)
        {
            item.Payload = payload;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<ProcessingJobItem>> SearchJobItemsAsync(
        string? searchText = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? status = null,
        string? itemType = null,
        int skip = 0,
        int take = 100)
    {
        var query = _context.ProcessingJobItems.AsQueryable();

        // Date range filter
        if (fromDate.HasValue)
        {
            query = query.Where(i => i.CreatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(i => i.CreatedAt <= toDate.Value);
        }

        // Status filter
        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(i => i.Status == status);
        }

        // Item type filter
        if (!string.IsNullOrEmpty(itemType))
        {
            query = query.Where(i => i.ItemType == itemType);
        }

        // Text search across ItemIdentifier, ErrorMessage, and GUIDs
        if (!string.IsNullOrEmpty(searchText))
        {
            var searchLower = searchText.ToLower();
            query = query.Where(i =>
                (i.ItemIdentifier != null && i.ItemIdentifier.ToLower().Contains(searchLower)) ||
                (i.ErrorMessage != null && i.ErrorMessage.ToLower().Contains(searchLower)) ||
                i.MessageId.ToString().ToLower().Contains(searchLower) ||
                i.JobId.ToString().ToLower().Contains(searchLower));
        }

        return await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<int> SearchJobItemsCountAsync(
        string? searchText = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? status = null,
        string? itemType = null)
    {
        var query = _context.ProcessingJobItems.AsQueryable();

        // Date range filter
        if (fromDate.HasValue)
        {
            query = query.Where(i => i.CreatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(i => i.CreatedAt <= toDate.Value);
        }

        // Status filter
        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(i => i.Status == status);
        }

        // Item type filter
        if (!string.IsNullOrEmpty(itemType))
        {
            query = query.Where(i => i.ItemType == itemType);
        }

        // Text search
        if (!string.IsNullOrEmpty(searchText))
        {
            var searchLower = searchText.ToLower();
            query = query.Where(i =>
                (i.ItemIdentifier != null && i.ItemIdentifier.ToLower().Contains(searchLower)) ||
                (i.ErrorMessage != null && i.ErrorMessage.ToLower().Contains(searchLower)) ||
                i.MessageId.ToString().ToLower().Contains(searchLower) ||
                i.JobId.ToString().ToLower().Contains(searchLower));
        }

        return await query.CountAsync();
    }
}
