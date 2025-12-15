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
            "Handling creation of Interaction '{InteractionName}' for Project {ProjectId}",
            message.InteractionName,
            message.ProjectId);

        try
        {
            var result = await _sharePointService.CreateInteractionAsync(message);

            if (result.Success && result.Data > 0)
            {
                // Update the message with the created folder ID
                message.CreatedSharePointFolderId = result.Data;

                _logger.LogInformation(
                    "Successfully created Interaction '{InteractionName}' with folder ID {FolderId}",
                    message.InteractionName,
                    result.Data);

                // TODO: Optionally update the database with the new folder ID
                // This would require knowing the interaction ID, which might need to be added to the message

                return OperationResult.SuccessResult();
            }
            else
            {
                _logger.LogWarning(
                    "Failed to create Interaction '{InteractionName}': {ErrorMessage}",
                    message.InteractionName,
                    result.ErrorMessage);
                return OperationResult.FailureResult(result.ErrorMessage ?? "Unknown error", result.ErrorCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Exception handling creation of Interaction '{InteractionName}'",
                message.InteractionName);
            return OperationResult.FailureResult(ex.Message);
        }
    }
}
