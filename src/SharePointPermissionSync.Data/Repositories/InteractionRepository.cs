using Microsoft.EntityFrameworkCore;
using SharePointPermissionSync.Data.Entities;

namespace SharePointPermissionSync.Data.Repositories;

/// <summary>
/// Repository for managing interactions and related entities
/// </summary>
public class InteractionRepository : IInteractionRepository
{
    private readonly ScyneShareContext _context;

    public InteractionRepository(ScyneShareContext context)
    {
        _context = context;
    }

    public async Task<Interaction?> GetByIdAsync(Guid id)
    {
        return await _context.Interactions
            .Include(i => i.Project)
            .Include(i => i.Engagement)
            .Include(i => i.InteractionMemberships)
            .FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task<Interaction?> GetBySharePointFolderIdAsync(int folderId)
    {
        return await _context.Interactions
            .Include(i => i.Project)
            .Include(i => i.Engagement)
            .FirstOrDefaultAsync(i => i.SharePointFolderID == folderId);
    }

    public async Task<List<Interaction>> GetByProjectIdAsync(Guid projectId)
    {
        return await _context.Interactions
            .Where(i => i.ProjectId == projectId && i.IsActive == true)
            .OrderBy(i => i.Name)
            .ToListAsync();
    }

    public async Task<List<Interaction>> GetByEngagementIdAsync(Guid engagementId)
    {
        return await _context.Interactions
            .Where(i => i.EngagementId == engagementId && i.IsActive == true)
            .OrderBy(i => i.Name)
            .ToListAsync();
    }

    public async Task UpdateSharePointFolderIdAsync(Guid interactionId, int folderId)
    {
        var interaction = await _context.Interactions.FindAsync(interactionId);
        if (interaction != null)
        {
            interaction.SharePointFolderID = folderId;
            interaction.ModifiedOn = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task UpdateUserListsAsync(Guid interactionId, string? internalUsers, string? externalUsers)
    {
        var interaction = await _context.Interactions.FindAsync(interactionId);
        if (interaction != null)
        {
            interaction.InternalUsers = internalUsers;
            interaction.ExternalUsers = externalUsers;
            interaction.ModifiedOn = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<Project?> GetProjectByIdAsync(Guid id)
    {
        return await _context.Projects
            .Include(p => p.Engagement)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Engagement?> GetEngagementByIdAsync(Guid id)
    {
        return await _context.Engagements
            .FirstOrDefaultAsync(e => e.Id == id);
    }
}
