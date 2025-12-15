namespace SharePointPermissionSync.Core.Configuration;

/// <summary>
/// Environment-specific SharePoint configuration
/// </summary>
public class EnvironmentSettings
{
    /// <summary>
    /// Azure AD Tenant ID
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Azure AD Client/Application ID
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Certificate thumbprint for authentication
    /// </summary>
    public string CertificateThumbprint { get; set; } = string.Empty;

    /// <summary>
    /// SharePoint tenant name (e.g., "scyneadvisory")
    /// </summary>
    public string TenantName { get; set; } = string.Empty;

    /// <summary>
    /// Database connection string for this environment
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;
}
