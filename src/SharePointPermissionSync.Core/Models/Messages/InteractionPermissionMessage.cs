namespace SharePointPermissionSync.Core.Models.Messages;

/// <summary>
/// Message for updating permissions on an existing interaction folder
/// </summary>
public class InteractionPermissionMessage : QueueMessageBase
{
    // === ENTITY IDENTIFIERS (GUID) ===
    /// <summary>
    /// Database ID of the interaction (GUID)
    /// </summary>
    public Guid InteractionId { get; set; }

    /// <summary>
    /// Database ID of the parent project (GUID)
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Database ID of the parent engagement (GUID)
    /// </summary>
    public Guid EngagementId { get; set; }

    // === SHAREPOINT FOLDER IDs (NON-NULLABLE - must exist for permission operations) ===
    /// <summary>
    /// SharePoint folder ID for the engagement (MUST exist)
    /// </summary>
    public int EngagementSharePointFolderId { get; set; }

    /// <summary>
    /// SharePoint folder ID for the project (MUST exist)
    /// </summary>
    public int ProjectSharePointFolderId { get; set; }

    /// <summary>
    /// SharePoint folder ID for the interaction (MUST exist)
    /// </summary>
    public int InteractionSharePointFolderId { get; set; }

    // === BUSINESS IDENTIFIERS ===
    /// <summary>
    /// Engagement name (for logging/debugging)
    /// </summary>
    public string EngagementName { get; set; } = string.Empty;

    /// <summary>
    /// Project name (for logging/debugging)
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Interaction name formatted as "00083 - Trading Operations Sample"
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

    // === PERMISSIONS ===
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
}
