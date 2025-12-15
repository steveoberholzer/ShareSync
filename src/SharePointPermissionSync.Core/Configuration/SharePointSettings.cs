namespace SharePointPermissionSync.Core.Configuration;

/// <summary>
/// SharePoint authentication and configuration settings
/// </summary>
public class SharePointSettings
{
    /// <summary>
    /// Environment-specific settings (DEV, UAT, PROD)
    /// </summary>
    public Dictionary<string, EnvironmentSettings> Environments { get; set; } = new();

    /// <summary>
    /// JSON template for interaction folder structure
    /// </summary>
    public string InteractionFolderTemplate { get; set; } =
        "{ \"Finding Documents\": {}, \"Response Documents\": {}, \"Template\": {}, \"Utility\": {} }";

    /// <summary>
    /// JSON template for project folder structure
    /// </summary>
    public string ProjectFolderTemplate { get; set; } =
        "{ \"Template\": {}, \"Utility\": {} }";

    /// <summary>
    /// JSON template for engagement folder structure
    /// </summary>
    public string EngagementFolderTemplate { get; set; } =
        "{ \"Template\": {}, \"Utility\": {} }";

    /// <summary>
    /// Get settings for a specific environment
    /// </summary>
    public EnvironmentSettings? GetEnvironment(string environment)
    {
        return Environments.TryGetValue(environment, out var settings) ? settings : null;
    }
}
