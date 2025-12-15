namespace SharePointPermissionSync.Core.Models.Messages;

/// <summary>
/// Represents a permission entry for a user
/// </summary>
public class PermissionEntry
{
    /// <summary>
    /// Email address of the user
    /// </summary>
    public string UserEmail { get; set; } = string.Empty;

    /// <summary>
    /// Permission level: "Read", "Contribute", "Full Control", etc.
    /// </summary>
    public string Role { get; set; } = string.Empty;
}
