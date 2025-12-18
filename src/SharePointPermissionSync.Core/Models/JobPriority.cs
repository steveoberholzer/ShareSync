namespace SharePointPermissionSync.Core.Models;

/// <summary>
/// Job priority levels
/// </summary>
public enum JobPriority
{
    Low = 1,
    Medium = 5,
    High = 10
}

/// <summary>
/// Helper methods for job priority
/// </summary>
public static class JobPriorityHelper
{
    /// <summary>
    /// Get priority value from string name
    /// </summary>
    public static int GetPriorityValue(string? priority)
    {
        return priority?.ToUpper() switch
        {
            "HIGH" => (int)JobPriority.High,
            "MEDIUM" => (int)JobPriority.Medium,
            "LOW" => (int)JobPriority.Low,
            _ => (int)JobPriority.Medium
        };
    }

    /// <summary>
    /// Get priority name from numeric value
    /// </summary>
    public static string GetPriorityName(int value)
    {
        return value switch
        {
            10 => "High",
            5 => "Medium",
            1 => "Low",
            _ => "Medium"
        };
    }
}
