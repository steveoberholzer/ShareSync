namespace SharePointPermissionSync.Core.Constants;

/// <summary>
/// SharePoint permission level constants
/// </summary>
public static class PermissionLevels
{
    /// <summary>
    /// Read-only access
    /// </summary>
    public const string Read = "Read";

    /// <summary>
    /// Edit/Contribute access
    /// </summary>
    public const string Contribute = "Contribute";

    /// <summary>
    /// Full Control access
    /// </summary>
    public const string FullControl = "Full Control";

    /// <summary>
    /// Restricted View access (limited read)
    /// </summary>
    public const string RestrictedView = "Restricted View";

    /// <summary>
    /// No access
    /// </summary>
    public const string None = "None";

    /// <summary>
    /// Maps common permission names to SharePoint permission levels
    /// </summary>
    public static string MapToSharePointPermission(string permission)
    {
        return permission?.ToLowerInvariant() switch
        {
            "read" => Read,
            "edit" or "contribute" => Contribute,
            "full" or "full control" => FullControl,
            "restricted" or "restricted view" => RestrictedView,
            "no access" or "none" => None,
            _ => permission ?? string.Empty
        };
    }

    /// <summary>
    /// Validates if a permission level is valid
    /// </summary>
    public static bool IsValid(string permission)
    {
        return permission switch
        {
            Read or Contribute or FullControl or RestrictedView or None => true,
            _ => false
        };
    }
}
