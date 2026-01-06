using SharePointPermissionSync.Core.Models.Messages;
using SharePointPermissionSync.Core.Utilities;
using SharePointPermissionSync.Worker.Services;

namespace SharePointPermissionSync.Worker.Handlers;

/// <summary>
/// Handler for interaction permission update operations
/// </summary>
public class InteractionPermissionHandler : IOperationHandler<InteractionPermissionMessage>
{
    private readonly SharePointOperationService _sharePointService;
    private readonly ILogger<InteractionPermissionHandler> _logger;

    public InteractionPermissionHandler(
        SharePointOperationService sharePointService,
        ILogger<InteractionPermissionHandler> logger)
    {
        _sharePointService = sharePointService;
        _logger = logger;
    }

    public async Task<OperationResult> HandleAsync(
        InteractionPermissionMessage message,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Handling permission update for Interaction '{InteractionName}' (ID: {InteractionId}, Folder: {FolderId})",
            message.InteractionName,
            message.InteractionId,
            message.InteractionSharePointFolderId);

        try
        {
            // Validate that all SharePoint folder IDs are present
            if (!SharePointNameHelper.ValidateSharePointFolderIds(
                message.EngagementSharePointFolderId,
                message.ProjectSharePointFolderId,
                message.InteractionSharePointFolderId))
            {
                string error = $"Missing SharePoint folder IDs for Interaction '{message.InteractionName}'. " +
                             $"Engagement: {message.EngagementSharePointFolderId}, " +
                             $"Project: {message.ProjectSharePointFolderId}, " +
                             $"Interaction: {message.InteractionSharePointFolderId}";
                _logger.LogError(error);
                return OperationResult.FailureResult(error, errorCode: 1002);
            }

            var result = await _sharePointService.ApplyInteractionPermissionsAsync(message);

            if (result.Success)
            {
                _logger.LogInformation(
                    "Successfully updated permissions for Interaction '{InteractionName}' (ID: {InteractionId})",
                    message.InteractionName,
                    message.InteractionId);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to update permissions for Interaction '{InteractionName}' (ID: {InteractionId}): {ErrorMessage}",
                    message.InteractionName,
                    message.InteractionId,
                    result.ErrorMessage);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Exception handling permission update for Interaction '{InteractionName}' (ID: {InteractionId})",
                message.InteractionName,
                message.InteractionId);
            return OperationResult.FailureResult(ex.Message);
        }
    }
}
