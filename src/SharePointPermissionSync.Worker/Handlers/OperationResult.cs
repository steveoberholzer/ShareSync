namespace SharePointPermissionSync.Worker.Handlers;

/// <summary>
/// Result of a SharePoint operation
/// </summary>
public class OperationResult
{
    /// <summary>
    /// Whether the operation succeeded
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error code from SharePoint service
    /// </summary>
    public int ErrorCode { get; set; }

    /// <summary>
    /// Error message if operation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Creates a successful result
    /// </summary>
    public static OperationResult SuccessResult() => new() { Success = true };

    /// <summary>
    /// Creates a failed result
    /// </summary>
    public static OperationResult FailureResult(string errorMessage, int errorCode = 0) =>
        new() { Success = false, ErrorMessage = errorMessage, ErrorCode = errorCode };
}

/// <summary>
/// Result of a SharePoint operation with data
/// </summary>
public class OperationResult<T> : OperationResult
{
    /// <summary>
    /// Data returned from the operation
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// Creates a successful result with data
    /// </summary>
    public static OperationResult<T> SuccessResult(T data) =>
        new() { Success = true, Data = data };

    /// <summary>
    /// Creates a failed result with data type
    /// </summary>
    public new static OperationResult<T> FailureResult(string errorMessage, int errorCode = 0) =>
        new() { Success = false, ErrorMessage = errorMessage, ErrorCode = errorCode };
}
