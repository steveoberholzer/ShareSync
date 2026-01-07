using SharePointPermissionSync.Core.Models.Messages;
using SharePointPermissionSync.Data.Repositories;
using SharePointPermissionSync.Worker.Services;

namespace SharePointPermissionSync.Worker.Handlers;

/// <summary>
/// Handler for interaction creation operations
/// </summary>
public class InteractionCreationHandler : IOperationHandler<InteractionCreationMessage>
{
    private readonly SharePointOperationService _sharePointService;
    private readonly IInteractionRepository _interactionRepository;
    private readonly ILogger<InteractionCreationHandler> _logger;

    public InteractionCreationHandler(
        SharePointOperationService sharePointService,
        IInteractionRepository interactionRepository,
        ILogger<InteractionCreationHandler> logger)
    {
        _sharePointService = sharePointService;
        _interactionRepository = interactionRepository;
        _logger = logger;
    }

    public async Task<OperationResult> HandleAsync(
        InteractionCreationMessage message,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Handling creation of Interaction '{InteractionName}' (ID: {InteractionId}) " +
            "for Project '{ProjectName}' (ID: {ProjectId})",
            message.InteractionName,
            message.InteractionId,
            message.ProjectName,
            message.ProjectId);

        try
        {
            // === VALIDATION: Check if Interaction folder already exists ===
            if (message.InteractionSharePointFolderId.HasValue &&
                message.InteractionSharePointFolderId.Value > 0)
            {
                _logger.LogWarning(
                    "Interaction '{InteractionName}' (ID: {InteractionId}) already has SharePoint folder ID {FolderId}. " +
                    "Skipping creation. Use InteractionPermission operation to update permissions on existing folders.",
                    message.InteractionName,
                    message.InteractionId,
                    message.InteractionSharePointFolderId.Value);

                return OperationResult.SuccessResult();
            }

            int projectSharePointFolderId;

            // === STEP 1: Check if Project folder exists, create if needed ===
            if (!message.ProjectSharePointFolderId.HasValue || message.ProjectSharePointFolderId.Value <= 0)
            {
                _logger.LogInformation(
                    "Project '{ProjectName}' (ID: {ProjectId}) has no SharePoint folder, creating...",
                    message.ProjectName,
                    message.ProjectId);

                // Validate that Engagement folder exists
                if (!message.EngagementSharePointFolderId.HasValue ||
                    message.EngagementSharePointFolderId.Value <= 0)
                {
                    string error = $"Cannot create Project '{message.ProjectName}': " +
                                 $"Engagement '{message.EngagementName}' has no SharePoint folder ID. " +
                                 "Engagement folders must be created first.";
                    _logger.LogError(error);
                    return OperationResult.FailureResult(error, errorCode: 1001);
                }

                // Create Project folder using SharePoint broker
                var projectResult = await _sharePointService.CreateProjectAsync(
                    message.SiteUrl,
                    message.DocumentLibrary,
                    message.EngagementSharePointFolderId.Value,
                    message.ProjectName,
                    engagementSubfolder: string.Empty);

                if (!projectResult.Success || projectResult.Data <= 0)
                {
                    string error = $"Failed to create Project folder '{message.ProjectName}': " +
                                 $"{projectResult.ErrorMessage ?? "Unknown error"}";
                    _logger.LogError(error);
                    return OperationResult.FailureResult(error, projectResult.ErrorCode);
                }

                projectSharePointFolderId = projectResult.Data;
                message.CreatedProjectSharePointFolderId = projectSharePointFolderId;

                // Update database with new Project SharePoint folder ID
                await _interactionRepository.UpdateProjectSharePointFolderIdAsync(
                    message.ProjectId,
                    projectSharePointFolderId);

                _logger.LogInformation(
                    "Successfully created Project folder '{ProjectName}' with SharePoint ID {FolderId}",
                    message.ProjectName,
                    projectSharePointFolderId);
            }
            else
            {
                projectSharePointFolderId = message.ProjectSharePointFolderId.Value;
                _logger.LogInformation(
                    "Project '{ProjectName}' already has SharePoint folder ID {FolderId}",
                    message.ProjectName,
                    projectSharePointFolderId);
            }

            // === STEP 2: Create Interaction folder ===
            _logger.LogInformation(
                "Creating Interaction folder '{InteractionName}' under Project folder {ProjectFolderId}",
                message.InteractionName,
                projectSharePointFolderId);

            var interactionResult = await _sharePointService.CreateInteractionAsync(
                message.SiteUrl,
                message.DocumentLibrary,
                projectSharePointFolderId,
                message.InteractionName,
                message.ProjectSubfolder,
                message.InternalPermission,
                message.InternalUserEmails,
                message.ExternalPermission,
                message.ExternalUserEmails);

            if (!interactionResult.Success || interactionResult.Data <= 0)
            {
                string error = $"Failed to create Interaction folder '{message.InteractionName}': " +
                             $"{interactionResult.ErrorMessage ?? "Unknown error"}";
                _logger.LogError(error);
                return OperationResult.FailureResult(error, interactionResult.ErrorCode);
            }

            int interactionSharePointFolderId = interactionResult.Data;
            message.CreatedInteractionSharePointFolderId = interactionSharePointFolderId;

            // === STEP 3: Update database with Interaction SharePoint folder ID ===
            await _interactionRepository.UpdateSharePointFolderIdAsync(
                message.InteractionId,
                interactionSharePointFolderId);

            _logger.LogInformation(
                "Successfully created Interaction '{InteractionName}' with SharePoint folder ID {FolderId}. " +
                "Database updated.",
                message.InteractionName,
                interactionSharePointFolderId);

            // === STEP 4: Optionally update user lists in database ===
            if (message.InternalUserEmails.Any() || message.ExternalUserEmails.Any())
            {
                string internalUsers = string.Join(";", message.InternalUserEmails);
                string externalUsers = string.Join(";", message.ExternalUserEmails);

                await _interactionRepository.UpdateUserListsAsync(
                    message.InteractionId,
                    internalUsers,
                    externalUsers);

                _logger.LogInformation(
                    "Updated user lists for Interaction {InteractionId}",
                    message.InteractionId);
            }

            return OperationResult.SuccessResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Exception handling creation of Interaction '{InteractionName}' (ID: {InteractionId})",
                message.InteractionName,
                message.InteractionId);
            return OperationResult.FailureResult(ex.Message);
        }
    }
}
