namespace SharePointPermissionSync.Core.Models.Messages;

/// <summary>
/// Message for creating a new interaction folder in SharePoint
/// </summary>
public class InteractionCreationMessage : QueueMessageBase
{
    // === ENTITY IDENTIFIERS (GUID) ===
    /// <summary>
    /// Database ID of the parent engagement (GUID)
    /// </summary>
    public Guid EngagementId { get; set; }

    /// <summary>
    /// Database ID of the parent project (GUID)
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Database ID of the interaction (GUID) - needed for updating after creation
    /// </summary>
    public Guid InteractionId { get; set; }

    // === SHAREPOINT FOLDER IDs (Nullable int) ===
    /// <summary>
    /// SharePoint folder ID for the engagement (null if not created yet)
    /// </summary>
    public int? EngagementSharePointFolderId { get; set; }

    /// <summary>
    /// SharePoint folder ID for the project (null if needs creation)
    /// </summary>
    public int? ProjectSharePointFolderId { get; set; }

    /// <summary>
    /// SharePoint folder ID for the interaction (null for new interactions)
    /// Should always be null for creation operations
    /// </summary>
    public int? InteractionSharePointFolderId { get; set; }

    // === BUSINESS IDENTIFIERS (Cleaned Names) ===
    /// <summary>
    /// Engagement name (cleaned for SharePoint compatibility)
    /// </summary>
    public string EngagementName { get; set; } = string.Empty;

    /// <summary>
    /// Project name (cleaned for SharePoint compatibility)
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Interaction name formatted as "00083 - Trading Operations Sample"
    /// (InteractionNumber padded with zeros - Name, both cleaned)
    /// </summary>
    public string InteractionName { get; set; } = string.Empty;

    // === SHAREPOINT CONFIGURATION ===
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

    // === PERMISSIONS ===
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

    // === RESULT DATA (populated after creation) ===
    /// <summary>
    /// SharePoint folder ID assigned to the project after successful creation
    /// (only if project was created during this operation)
    /// </summary>
    public int? CreatedProjectSharePointFolderId { get; set; }

    /// <summary>
    /// SharePoint folder ID assigned to the interaction after successful creation
    /// </summary>
    public int? CreatedInteractionSharePointFolderId { get; set; }
}
