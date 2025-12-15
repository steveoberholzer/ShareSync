namespace SharePointPermissionSync.Core.Configuration;

/// <summary>
/// Configuration for message processing and throttling
/// </summary>
public class ProcessingSettings
{
    /// <summary>
    /// Default delay between messages in milliseconds
    /// </summary>
    public int DefaultDelay { get; set; } = 500;

    /// <summary>
    /// Minimum delay between messages in milliseconds
    /// </summary>
    public int MinDelay { get; set; } = 50;

    /// <summary>
    /// Maximum delay between messages in milliseconds
    /// </summary>
    public int MaxDelay { get; set; } = 5000;

    /// <summary>
    /// Maximum number of retries for failed operations
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Number of messages to process in a batch
    /// </summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>
    /// Number of consecutive successes before reducing delay
    /// </summary>
    public int SuccessThreshold { get; set; } = 10;

    /// <summary>
    /// Delay reduction factor (0.0 - 1.0)
    /// </summary>
    public double DelayReductionFactor { get; set; } = 0.9;

    /// <summary>
    /// Delay increase multiplier when throttled
    /// </summary>
    public int ThrottleMultiplier { get; set; } = 2;
}
