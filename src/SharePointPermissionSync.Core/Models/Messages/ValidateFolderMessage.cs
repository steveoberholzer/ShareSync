namespace SharePointPermissionSync.Core.Models.Messages;

/// <summary>
/// Message for validating the existence of folders in SharePoint
/// </summary>
public class ValidateFolderMessage : QueueMessageBase
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
    /// List of SharePoint folder IDs to validate
    /// </summary>
    public List<int> FolderIds { get; set; } = new();
}
