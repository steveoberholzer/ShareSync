using SharePointPermissionSync.Core.Models.Messages;

namespace SharePointPermissionSync.Worker.Handlers;

/// <summary>
/// Interface for operation handlers
/// </summary>
public interface IOperationHandler<TMessage> where TMessage : QueueMessageBase
{
    /// <summary>
    /// Handles a queue message
    /// </summary>
    Task<OperationResult> HandleAsync(TMessage message, CancellationToken cancellationToken = default);
}
