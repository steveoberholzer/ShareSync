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
}
