using SharePointPermissionSync.Core.Configuration;
using SharePointPermissionSync.Core.Models.Messages;
using SharePointPermissionSync.Worker.Handlers;
using SourceCode.SmartObjects.Services.ServiceSDK.Objects;
using Tecala.SMO.SharePoint;

namespace SharePointPermissionSync.Worker.Services;

/// <summary>
/// Service for performing SharePoint operations using Tecala.SMO.SharePoint broker
/// </summary>
public class SharePointOperationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SharePointOperationService> _logger;
    private ServiceConfiguration? _serviceConfig;
    private readonly string _environment;

    public SharePointOperationService(
        IConfiguration configuration,
        ILogger<SharePointOperationService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _environment = _configuration["Environment"] ?? "DEV";
        InitializeServiceConfiguration();
    }

    private void InitializeServiceConfiguration()
    {
        _serviceConfig = new ServiceConfiguration();

        // Add Entra (Azure AD) configuration
        _serviceConfig.Add("Entra-TenantId", false,
            _configuration[$"SharePoint:{_environment}:TenantId"] ?? string.Empty);
        _serviceConfig.Add("Entra-ClientId", false,
            _configuration[$"SharePoint:{_environment}:ClientId"] ?? string.Empty);
        _serviceConfig.Add("Entra-CertificateThumbprint", false,
            _configuration[$"SharePoint:{_environment}:CertificateThumbprint"] ?? string.Empty);

        // Add SharePoint configuration
        _serviceConfig.Add("SharePoint-TenantName", false,
            _configuration[$"SharePoint:{_environment}:TenantName"] ?? string.Empty);

        // Add folder templates
        _serviceConfig.Add("InteractionFolderTemplateJSON", false,
            _configuration["SharePoint:InteractionFolderTemplate"] ?? string.Empty);
        _serviceConfig.Add("ProjectFolderTemplateJSON", false,
            _configuration["SharePoint:ProjectFolderTemplate"] ?? string.Empty);
        _serviceConfig.Add("EngagementFolderTemplateJSON", false,
            _configuration["SharePoint:EngagementFolderTemplate"] ?? string.Empty);

        // Add database connection
        _serviceConfig.Add("ScyneDB-ConnectionString", false,
            _configuration["ConnectionStrings:DefaultConnection"] ?? string.Empty);

        _logger.LogInformation("SharePoint service configuration initialized for environment: {Environment}", _environment);
    }

    /// <summary>
    /// Apply permissions to an existing interaction folder
    /// Uses ParticipantChangeInteraction for specific users instead of PermissionChangeInteraction
    /// which would affect all users and cascade to all subfolders
    /// </summary>
    public async Task<OperationResult> ApplyInteractionPermissionsAsync(
        InteractionPermissionMessage message)
    {
        return await Task.Run(() =>
        {
            try
            {
                _logger.LogInformation(
                    "Applying permissions for Interaction '{InteractionName}' (ID: {InteractionId}, Folder: {FolderId})",
                    message.InteractionName,
                    message.InteractionId,
                    message.InteractionSharePointFolderId);

                // Apply internal permissions if users are specified
                if (message.InternalUserEmails != null && message.InternalUserEmails.Any())
                {
                    _logger.LogInformation(
                        "Applying internal permissions ({Permission}) for {Count} users",
                        message.InternalPermission,
                        message.InternalUserEmails.Count);

                    var internalService = new SharePointService
                    {
                        ServiceConfiguration = _serviceConfig!,
                        SiteUrl = message.SiteUrl,
                        DocumentLibrary = message.DocumentLibrary,
                        InteractionID = message.InteractionSharePointFolderId,
                        ListOfEmailAddresses = string.Join(";", message.InternalUserEmails),
                        Permission = message.InternalPermission
                    };

                    internalService = internalService.ParticipantChangeInteraction();

                    if (!internalService.Success)
                    {
                        _logger.LogWarning(
                            "Failed to apply internal permissions: {ErrorMessage}",
                            internalService.ErrorMessage);
                        return OperationResult.FailureResult(
                            $"Failed to apply internal permissions: {internalService.ErrorMessage}",
                            internalService.ErrorNumber);
                    }

                    _logger.LogInformation("Successfully applied internal permissions");
                }

                // Apply external permissions if users are specified
                if (!string.IsNullOrWhiteSpace(message.ExternalPermission) &&
                    message.ExternalUserEmails != null &&
                    message.ExternalUserEmails.Any())
                {
                    _logger.LogInformation(
                        "Applying external permissions ({Permission}) for {Count} users",
                        message.ExternalPermission,
                        message.ExternalUserEmails.Count);

                    var externalService = new SharePointService
                    {
                        ServiceConfiguration = _serviceConfig!,
                        SiteUrl = message.SiteUrl,
                        DocumentLibrary = message.DocumentLibrary,
                        InteractionID = message.InteractionSharePointFolderId,
                        ListOfEmailAddresses = string.Join(";", message.ExternalUserEmails),
                        Permission = message.ExternalPermission
                    };

                    externalService = externalService.ParticipantChangeInteraction();

                    if (!externalService.Success)
                    {
                        _logger.LogWarning(
                            "Failed to apply external permissions: {ErrorMessage}",
                            externalService.ErrorMessage);
                        return OperationResult.FailureResult(
                            $"Failed to apply external permissions: {externalService.ErrorMessage}",
                            externalService.ErrorNumber);
                    }

                    _logger.LogInformation("Successfully applied external permissions");
                }

                _logger.LogInformation(
                    "Successfully applied all permissions for Interaction '{InteractionName}' (ID: {InteractionId})",
                    message.InteractionName,
                    message.InteractionId);

                return OperationResult.SuccessResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Exception applying permissions for Interaction '{InteractionName}' (ID: {InteractionId})",
                    message.InteractionName,
                    message.InteractionId);
                return OperationResult.FailureResult(ex.Message);
            }
        });
    }

    /// <summary>
    /// Create a new interaction folder with permissions
    /// Now accepts ProjectSharePointFolderId directly instead of deriving from ProjectId
    /// </summary>
    public async Task<OperationResult<int>> CreateInteractionAsync(
        string siteUrl,
        string documentLibrary,
        int projectSharePointFolderId,
        string interactionName,
        string projectSubfolder,
        string internalPermission,
        List<string> internalUserEmails,
        string externalPermission,
        List<string> externalUserEmails)
    {
        return await Task.Run(() =>
        {
            try
            {
                _logger.LogInformation(
                    "Creating Interaction '{InteractionName}' under Project folder {ProjectFolderId}",
                    interactionName,
                    projectSharePointFolderId);

                var service = new SharePointService
                {
                    ServiceConfiguration = _serviceConfig!,
                    SiteUrl = siteUrl,
                    DocumentLibrary = documentLibrary,
                    ProjectID = projectSharePointFolderId, // Now using SharePoint folder ID directly
                    InteractionName = interactionName,
                    ProjectSubfolder = projectSubfolder ?? string.Empty,
                    InternalPermission = internalPermission,
                    ListOfInternalEmailAddresses = string.Join(";", internalUserEmails),
                    ExternalPermission = externalPermission ?? string.Empty,
                    ListOfExternalEmailAddresses = string.Join(";", externalUserEmails)
                };

                // Call the broker method - it returns the service with updated properties
                service = service.NewInteraction();

                if (service.Success)
                {
                    // After NewInteraction(), the folder ID is in InteractionID property, not ID
                    int folderId = service.InteractionID;
                    _logger.LogInformation(
                        "Successfully created Interaction '{InteractionName}' with folder ID {FolderId}",
                        interactionName,
                        folderId);
                    return OperationResult<int>.SuccessResult(folderId);
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to create Interaction '{InteractionName}': {ErrorMessage}",
                        interactionName,
                        service.ErrorMessage);
                    return OperationResult<int>.FailureResult(
                        service.ErrorMessage ?? "Unknown error",
                        service.ErrorNumber);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Exception creating Interaction '{InteractionName}'",
                    interactionName);
                return OperationResult<int>.FailureResult(ex.Message);
            }
        });
    }

    /// <summary>
    /// Close an interaction (remove permissions)
    /// </summary>
    public async Task<OperationResult> CloseInteractionAsync(
        string siteUrl,
        string documentLibrary,
        int interactionId)
    {
        return await Task.Run(() =>
        {
            try
            {
                _logger.LogInformation(
                    "Closing Interaction {InteractionId}",
                    interactionId);

                var service = new SharePointService
                {
                    ServiceConfiguration = _serviceConfig!,
                    SiteUrl = siteUrl,
                    DocumentLibrary = documentLibrary,
                    InteractionID = interactionId
                };

                // Call the broker method
                service.CloseInteraction();

                if (service.Success)
                {
                    _logger.LogInformation(
                        "Successfully closed Interaction {InteractionId}",
                        interactionId);
                    return OperationResult.SuccessResult();
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to close Interaction {InteractionId}: {ErrorMessage}",
                        interactionId,
                        service.ErrorMessage);
                    return OperationResult.FailureResult(
                        service.ErrorMessage ?? "Unknown error",
                        service.ErrorNumber);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Exception closing Interaction {InteractionId}",
                    interactionId);
                return OperationResult.FailureResult(ex.Message);
            }
        });
    }

    /// <summary>
    /// Create a new project folder
    /// Uses Tecala.SMO.SharePoint broker's NewProject method
    /// </summary>
    public async Task<OperationResult<int>> CreateProjectAsync(
        string siteUrl,
        string documentLibrary,
        int engagementSharePointFolderId,
        string projectName,
        string engagementSubfolder = "")
    {
        return await Task.Run(() =>
        {
            try
            {
                _logger.LogInformation(
                    "Creating Project '{ProjectName}' under Engagement folder {EngagementId}",
                    projectName,
                    engagementSharePointFolderId);

                var service = new SharePointService
                {
                    ServiceConfiguration = _serviceConfig!,
                    SiteUrl = siteUrl,
                    DocumentLibrary = documentLibrary,
                    EngagementID = engagementSharePointFolderId,
                    ProjectName = projectName,
                    EngagementSubfolder = engagementSubfolder ?? string.Empty
                };

                // Call the broker method - it returns the service with updated properties
                service = service.NewProject();

                if (service.Success)
                {
                    // After NewProject(), the folder ID is in ProjectID property, not ID
                    int folderId = service.ProjectID;
                    _logger.LogInformation(
                        "Successfully created Project '{ProjectName}' with folder ID {FolderId}",
                        projectName,
                        folderId);
                    return OperationResult<int>.SuccessResult(folderId);
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to create Project '{ProjectName}': {ErrorMessage}",
                        projectName,
                        service.ErrorMessage);
                    return OperationResult<int>.FailureResult(
                        service.ErrorMessage ?? "Unknown error",
                        service.ErrorNumber);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Exception creating Project '{ProjectName}'",
                    projectName);
                return OperationResult<int>.FailureResult(ex.Message);
            }
        });
    }
}
