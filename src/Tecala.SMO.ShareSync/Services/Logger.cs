using System;
using System.Diagnostics;

namespace Tecala.SMO.ShareSync.Services
{
    public class Logger : ILogger, IDisposable
    {
        private const string LogName = "Application";
        private const string SourceName = "Tecala.SMO.ShareSync";
        private readonly EventLog eventLog;

        public Logger()
        {
            if (!EventLog.SourceExists(SourceName))
                EventLog.CreateEventSource(SourceName, LogName);

            eventLog = new EventLog(LogName)
            {
                Source = SourceName
            };
        }

        public void LogTrace(string message)
        {
            WriteLog(message, EventLogEntryType.Information, 1000);
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        public void LogInformation(string message)
        {
            WriteLog(message, EventLogEntryType.Information, 1001);
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        public void LogWarning(string message)
        {
            WriteLog(message, EventLogEntryType.Warning, 1002);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        public void LogError(Exception ex, string message)
        {
            string fullMessage = $"{message}\r\nThe following exception occurred: {ex}";
            WriteLog(fullMessage, EventLogEntryType.Error, 1003);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(fullMessage);
            Console.ResetColor();
        }

        private void WriteLog(string message, EventLogEntryType entryType, int eventId)
        {
            try
            {
                eventLog.WriteEntry($"{DateTime.Now}: {message}", entryType, eventId);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to write to Event Log: {ex}");
            }
        }

        public void Dispose()
        {
            eventLog?.Dispose();
        }
    }
}
