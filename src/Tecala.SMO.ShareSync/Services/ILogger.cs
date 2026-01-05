using System;

namespace Tecala.SMO.ShareSync.Services
{
    public interface ILogger
    {
        void LogTrace(string message);
        void LogInformation(string message);
        void LogWarning(string message);
        void LogError(Exception ex, string message);
    }
}
