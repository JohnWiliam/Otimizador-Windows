using System;
using System.IO;

namespace SystemOptimizer.Helpers;

public static class Logger
{
    private static readonly string LogFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "system_optimizer_log.txt");
    private static readonly object _lock = new();

    public static void Log(string message, string type = "INFO")
    {
        try
        {
            lock (_lock)
            {
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{type}] {message}{Environment.NewLine}";
                File.AppendAllText(LogFile, logEntry);
            }
        }
        catch
        {
            // Fail silently if we can't write to the log
        }
    }
}
