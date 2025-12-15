namespace SharePointPermissionSync.Core.Models.Messages;

/// <summary>
/// Message for removing unique permissions from a folder (reset to inherit from parent)
/// </summary>
public class RemoveUniquePermissionMessage : QueueMessageBase
{
    /// <summary>
    /// SharePoint site URL
    /// </summary>
    public string SiteUrl { get; set; } = string.Empty;

    /// <summary>
    /// Document library name (typically "Documents")
    /// </summary>
    public string DocumentLibrary { get; set; } = "Documents";

    /// <summary>
    /// SharePoint folder ID to reset permissions
    /// </summary>
    public int FolderId { get; set; }

    /// <summary>
    /// Type of folder: "Interaction", "Project", "Engagement", or "Folder"
    /// </summary>
    public string FolderType { get; set; } = string.Empty;
}
