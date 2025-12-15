namespace SharePointPermissionSync.Core.Models.Messages;

/// <summary>
/// Message for updating permissions on an existing interaction folder
/// </summary>
public class InteractionPermissionMessage : QueueMessageBase
{
    /// <summary>
    /// Database ID of the interaction
    /// </summary>
    public int InteractionId { get; set; }

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
    /// SharePoint folder ID for the interaction
    /// </summary>
    public int SharePointFolderId { get; set; }

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
