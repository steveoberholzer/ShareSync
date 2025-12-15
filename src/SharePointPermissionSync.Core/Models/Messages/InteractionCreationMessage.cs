namespace SharePointPermissionSync.Core.Models.Messages;

/// <summary>
/// Message for creating a new interaction folder in SharePoint
/// </summary>
public class InteractionCreationMessage : QueueMessageBase
{
    /// <summary>
    /// Name of the interaction to create
    /// </summary>
    public string InteractionName { get; set; } = string.Empty;

    /// <summary>
    /// Database ID of the parent project
    /// </summary>
    public int ProjectId { get; set; }

    /// <summary>
    /// Database ID of the parent engagement
    /// </summary>
    public int EngagementId { get; set; }

    /// <summary>
    /// SharePoint site URL
    /// </summary>
    public string SiteUrl { get; set; } = string.Empty;

    /// <summary>
    /// Document library name (typically "Documents")
    /// </summary>
    public string DocumentLibrary { get; set; } = "Documents";

    /// <summary>
    /// Optional subfolder path within the project
    /// </summary>
    public string ProjectSubfolder { get; set; } = string.Empty;

    /// <summary>
    /// Email of the user creating this interaction
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Permission level for internal users (Read, Contribute, Full Control)
    /// </summary>
    public string InternalPermission { get; set; } = string.Empty;

    /// <summary>
    /// List of internal user email addresses
    /// </summary>
    public List<string> InternalUserEmails { get; set; } = new();

    /// <summary>
    /// Permission level for external users
    /// </summary>
    public string ExternalPermission { get; set; } = string.Empty;

    /// <summary>
    /// List of external user email addresses
    /// </summary>
    public List<string> ExternalUserEmails { get; set; } = new();

    /// <summary>
    /// SharePoint folder ID assigned after successful creation
    /// </summary>
    public int? CreatedSharePointFolderId { get; set; }
}
