namespace SharePointPermissionSync.Core.Models.Enums;

/// <summary>
/// Type of SharePoint operation to perform
/// </summary>
public enum OperationType
{
    /// <summary>
    /// Update permissions on an existing interaction folder
    /// </summary>
    InteractionPermissionSync,

    /// <summary>
    /// Create a new interaction folder with permissions
    /// </summary>
    InteractionCreation,

    /// <summary>
    /// Remove unique permissions from a folder (reset to inherit from parent)
    /// </summary>
    RemoveUniquePermission,

    /// <summary>
    /// Validate that folders exist in SharePoint
    /// </summary>
    ValidateFolder,

    /// <summary>
    /// Update permissions on a project folder
    /// </summary>
    ProjectPermissionSync,

    /// <summary>
    /// Update permissions on an engagement folder
    /// </summary>
    EngagementPermissionSync
}
