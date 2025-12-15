namespace SharePointPermissionSync.Core.Constants;

/// <summary>
/// Standard queue names used throughout the application
/// </summary>
public static class QueueNames
{
    /// <summary>
    /// Queue for interaction permission sync operations
    /// </summary>
    public const string InteractionPermissions = "sharepoint.interaction.permissions";

    /// <summary>
    /// Queue for interaction creation operations
    /// </summary>
    public const string InteractionCreation = "sharepoint.interaction.creation";

    /// <summary>
    /// Queue for removing unique permissions operations
    /// </summary>
    public const string RemovePermissions = "sharepoint.remove.permissions";

    /// <summary>
    /// Queue for folder validation operations
    /// </summary>
    public const string ValidateFolder = "sharepoint.validate.folder";

    /// <summary>
    /// Dead letter queue for failed messages
    /// </summary>
    public const string DeadLetter = "sharepoint.deadletter";
}
