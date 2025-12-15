using SharePointPermissionSync.Core.Models.Messages;
using SharePointPermissionSync.Worker.Services;

namespace SharePointPermissionSync.Worker.Handlers;

/// <summary>
/// Handler for removing unique permissions from folders
/// </summary>
public class RemoveUniquePermissionHandler : IOperationHandler<RemoveUniquePermissionMessage>
{
    private readonly SharePointOperationService _sharePointService;
    private readonly ILogger<RemoveUniquePermissionHandler> _logger;

    public RemoveUniquePermissionHandler(
        SharePointOperationService sharePointService,
        ILogger<RemoveUniquePermissionHandler> logger)
    {
        _sharePointService = sharePointService;
        _logger = logger;
    }

    public async Task<OperationResult> HandleAsync(
        RemoveUniquePermissionMessage message,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Handling removal of unique permissions for {FolderType} folder {FolderId}",
            message.FolderType,
            message.FolderId);

        try
        {
            // For interactions, we can use CloseInteraction which removes permissions
            if (message.FolderType == "Interaction")
            {
                var result = await _sharePointService.CloseInteractionAsync(
                    message.SiteUrl,
                    message.DocumentLibrary,
                    message.FolderId);

                if (result.Success)
                {
                    _logger.LogInformation(
                        "Successfully removed permissions for Interaction folder {FolderId}",
                        message.FolderId);
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to remove permissions for Interaction folder {FolderId}: {ErrorMessage}",
                        message.FolderId,
                        result.ErrorMessage);
                }

                return result;
            }
            else
            {
                // TODO: For other folder types, implement direct CSOM ResetRoleInheritance
                // This requires DirectSharePointService with CSOM operations
                _logger.LogWarning(
                    "Remove unique permissions not yet implemented for folder type: {FolderType}",
                    message.FolderType);
                return OperationResult.FailureResult(
                    $"Remove unique permissions not implemented for {message.FolderType}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Exception removing permissions for folder {FolderId}",
                message.FolderId);
            return OperationResult.FailureResult(ex.Message);
        }
    }
}
