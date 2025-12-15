namespace SharePointPermissionSync.Core.Constants;

/// <summary>
/// Environment name constants
/// </summary>
public static class Environments
{
    /// <summary>
    /// Development environment
    /// </summary>
    public const string Development = "DEV";

    /// <summary>
    /// User Acceptance Testing environment
    /// </summary>
    public const string UAT = "UAT";

    /// <summary>
    /// Production environment
    /// </summary>
    public const string Production = "PROD";

    /// <summary>
    /// Validates if an environment name is valid
    /// </summary>
    public static bool IsValid(string environment)
    {
        return environment switch
        {
            Development or UAT or Production => true,
            _ => false
        };
    }

    /// <summary>
    /// Gets all valid environment names
    /// </summary>
    public static string[] GetAll() => new[] { Development, UAT, Production };
}
