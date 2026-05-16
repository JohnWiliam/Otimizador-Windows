using System;
using System.IO;
using System.Text;

namespace SystemOptimizer.Helpers;

public static class Logger
{
    private const long MaxLogBytes = 1024 * 1024;
    private const long TrimmedLogBytes = 512 * 1024;
    private static readonly object LockObj = new();

    private static readonly string LogFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SystemOptimizer",
        "app.log");

    public static void Log(string message, string level = "INFO")
    {
        try
        {
            lock (LockObj)
            {
                var dir = Path.GetDirectoryName(LogFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                RotateIfNeeded();
                File.AppendAllText(LogFilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}", Encoding.UTF8);
            }
        }
        catch
        {
            // Logging nunca deve derrubar o app.
        }
    }

    private static void RotateIfNeeded()
    {
        var file = new FileInfo(LogFilePath);
        if (!file.Exists || file.Length < MaxLogBytes)
        {
            return;
        }

        string archivePath = LogFilePath + ".1";
        try
        {
            if (File.Exists(archivePath)) File.Delete(archivePath);

            if (file.Length <= TrimmedLogBytes)
            {
                File.Move(LogFilePath, archivePath);
                return;
            }

            using var input = new FileStream(LogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            input.Seek(-TrimmedLogBytes, SeekOrigin.End);
            using var archive = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            input.CopyTo(archive);
            File.WriteAllText(LogFilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] Log rotacionado; últimas entradas preservadas em app.log.1{Environment.NewLine}", Encoding.UTF8);
        }
        catch
        {
            File.WriteAllText(LogFilePath, string.Empty, Encoding.UTF8);
        }
    }
}
