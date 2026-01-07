namespace SharePointPermissionSync.Core.Utilities;

/// <summary>
/// Utility for cleaning and formatting names for SharePoint compatibility
/// </summary>
public static class SharePointNameHelper
{
    // SharePoint illegal characters: \ / : * ? " < > | # { } % ~ &
    private static readonly char[] IllegalChars = new[]
    {
        '\\', '/', ':', '*', '?', '"', '<', '>', '|', '#', '{', '}', '%', '~', '&'
    };

    /// <summary>
    /// Clean a folder name to make it SharePoint-compatible
    /// Removes illegal characters, trims whitespace, and ensures max length
    /// </summary>
    /// <param name="name">The name to clean</param>
    /// <param name="maxLength">Maximum allowed length (default 256 for SharePoint)</param>
    /// <returns>Cleaned name safe for SharePoint</returns>
    public static string CleanFolderName(string? name, int maxLength = 256)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        // Remove illegal characters
        string cleaned = name;
        foreach (char illegalChar in IllegalChars)
        {
            cleaned = cleaned.Replace(illegalChar.ToString(), string.Empty);
        }

        // Replace multiple spaces with single space
        while (cleaned.Contains("  "))
        {
            cleaned = cleaned.Replace("  ", " ");
        }

        // Trim and enforce max length
        cleaned = cleaned.Trim();
        if (cleaned.Length > maxLength)
        {
            cleaned = cleaned.Substring(0, maxLength).TrimEnd();
        }

        return cleaned;
    }

    /// <summary>
    /// Format interaction name using SharePoint standardized naming convention
    /// Example: InteractionNumber=1 => "Interaction1-R"
    ///          InteractionNumber=16 => "Interaction16-R"
    /// </summary>
    /// <param name="interactionNumber">The interaction number</param>
    /// <param name="name">The interaction name (not used, kept for compatibility)</param>
    /// <param name="paddingWidth">Not used, kept for compatibility</param>
    /// <returns>Formatted interaction folder name</returns>
    public static string FormatInteractionName(int? interactionNumber, string? name, int paddingWidth = 5)
    {
        if (!interactionNumber.HasValue || interactionNumber.Value <= 0)
            return CleanFolderName(name);

        return $"Interaction{interactionNumber.Value}-R";
    }

    /// <summary>
    /// Validate that all SharePoint folder IDs are present (non-null and > 0)
    /// Used for permission messages where folders must exist
    /// </summary>
    public static bool ValidateSharePointFolderIds(int? engagementId, int? projectId, int? interactionId)
    {
        return engagementId.HasValue && engagementId.Value > 0
            && projectId.HasValue && projectId.Value > 0
            && interactionId.HasValue && interactionId.Value > 0;
    }
}
