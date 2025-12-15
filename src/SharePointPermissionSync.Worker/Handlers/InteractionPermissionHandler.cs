using SharePointPermissionSync.Core.Models.Messages;
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
            "Handling permission update for Interaction {InteractionId} (Folder: {FolderId})",
            message.InteractionId,
            message.SharePointFolderId);

        try
        {
            var result = await _sharePointService.ApplyInteractionPermissionsAsync(message);

            if (result.Success)
            {
                _logger.LogInformation(
                    "Successfully updated permissions for Interaction {InteractionId}",
                    message.InteractionId);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to update permissions for Interaction {InteractionId}: {ErrorMessage}",
                    message.InteractionId,
                    result.ErrorMessage);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Exception handling permission update for Interaction {InteractionId}",
                message.InteractionId);
            return OperationResult.FailureResult(ex.Message);
        }
    }
}
