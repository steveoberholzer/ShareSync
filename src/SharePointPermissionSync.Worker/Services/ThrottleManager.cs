using SharePointPermissionSync.Core.Configuration;

namespace SharePointPermissionSync.Worker.Services;

/// <summary>
/// Manages adaptive throttling for SharePoint operations
/// </summary>
public class ThrottleManager
{
    private int _currentDelay;
    private readonly int _minDelay;
    private readonly int _maxDelay;
    private readonly int _successThreshold;
    private readonly double _delayReductionFactor;
    private readonly int _throttleMultiplier;

    private int _successCount = 0;
    private int _throttleCount = 0;

    public ThrottleManager(ProcessingSettings settings)
    {
        _currentDelay = settings.DefaultDelay;
        _minDelay = settings.MinDelay;
        _maxDelay = settings.MaxDelay;
        _successThreshold = settings.SuccessThreshold;
        _delayReductionFactor = settings.DelayReductionFactor;
        _throttleMultiplier = settings.ThrottleMultiplier;
    }

    /// <summary>
    /// Current delay in milliseconds
    /// </summary>
    public int CurrentDelay => _currentDelay;

    /// <summary>
    /// Number of consecutive successes
    /// </summary>
    public int SuccessCount => _successCount;

    /// <summary>
    /// Number of times throttled
    /// </summary>
    public int ThrottleCount => _throttleCount;

    /// <summary>
    /// Report a successful operation
    /// </summary>
    public void ReportSuccess()
    {
        _successCount++;

        // After threshold consecutive successes, reduce delay
        if (_successCount >= _successThreshold)
        {
            _currentDelay = Math.Max(_minDelay, (int)(_currentDelay * _delayReductionFactor));
            _successCount = 0;
        }
    }

    /// <summary>
    /// Report a throttling event (429 error)
    /// </summary>
    public void ReportThrottling()
    {
        _throttleCount++;
        _successCount = 0; // Reset success counter

        // Increase delay by multiplier, up to max
        _currentDelay = Math.Min(_maxDelay, _currentDelay * _throttleMultiplier);
    }

    /// <summary>
    /// Get current throttle statistics
    /// </summary>
    public ThrottleStats GetStats()
    {
        return new ThrottleStats
        {
            CurrentDelay = _currentDelay,
            SuccessCount = _successCount,
            ThrottleCount = _throttleCount
        };
    }

    /// <summary>
    /// Reset throttle statistics
    /// </summary>
    public void Reset()
    {
        _successCount = 0;
        _throttleCount = 0;
    }
}

/// <summary>
/// Throttle statistics
/// </summary>
public class ThrottleStats
{
    public int CurrentDelay { get; set; }
    public int SuccessCount { get; set; }
    public int ThrottleCount { get; set; }
}
