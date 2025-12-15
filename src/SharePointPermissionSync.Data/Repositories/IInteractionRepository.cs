using SharePointPermissionSync.Data.Entities;

namespace SharePointPermissionSync.Data.Repositories;

/// <summary>
/// Repository interface for managing interactions
/// </summary>
public interface IInteractionRepository
{
    /// <summary>
    /// Get an interaction by ID
    /// </summary>
    Task<Interaction?> GetByIdAsync(Guid id);

    /// <summary>
    /// Get an interaction by SharePoint folder ID
    /// </summary>
    Task<Interaction?> GetBySharePointFolderIdAsync(int folderId);

    /// <summary>
    /// Get interactions for a project
    /// </summary>
    Task<List<Interaction>> GetByProjectIdAsync(Guid projectId);

    /// <summary>
    /// Get interactions for an engagement
    /// </summary>
    Task<List<Interaction>> GetByEngagementIdAsync(Guid engagementId);

    /// <summary>
    /// Update interaction's SharePoint folder ID
    /// </summary>
    Task UpdateSharePointFolderIdAsync(Guid interactionId, int folderId);

    /// <summary>
    /// Update interaction's user lists
    /// </summary>
    Task UpdateUserListsAsync(Guid interactionId, string? internalUsers, string? externalUsers);

    /// <summary>
    /// Get a project by ID
    /// </summary>
    Task<Project?> GetProjectByIdAsync(Guid id);

    /// <summary>
    /// Get an engagement by ID
    /// </summary>
    Task<Engagement?> GetEngagementByIdAsync(Guid id);
}
