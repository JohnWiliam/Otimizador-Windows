using System;
using System.IO;

namespace SystemOptimizer.Helpers;

public static class Logger
{
    private const long MaxLogBytes = 1024 * 1024;
    private const int MaxArchiveFiles = 3;
    private static readonly object SyncRoot = new();

    // Define o caminho fixo: C:\ProgramData\SystemOptimizer\system_optimizer_log.txt
    private static readonly string LogFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), 
        "SystemOptimizer");
    
    private static readonly string LogFile = Path.Combine(LogFolder, "system_optimizer_log.txt");

    // Construtor estático para garantir que a pasta existe antes de qualquer log
    static Logger()
    {
        try
        {
            if (!Directory.Exists(LogFolder))
            {
                Directory.CreateDirectory(LogFolder);
            }
        }
        catch
        {
            // Se falhar ao criar a pasta (ex: falta de permissão), 
            // falharemos silenciosamente para não travar o app no início.
        }
    }

    public static void Log(string message, string type = "INFO")
    {
        try
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string logEntry = $"[{timestamp}] [{type}] {message}{Environment.NewLine}";

            lock (SyncRoot)
            {
                RotateIfNeeded();
                File.AppendAllText(LogFile, logEntry);
            }
        }
        catch
        {
            // Ignora erros de gravação de log (ex: arquivo em uso)
        }
    }

    private static void RotateIfNeeded()
    {
        try
        {
            var logInfo = new FileInfo(LogFile);
            if (!logInfo.Exists || logInfo.Length < MaxLogBytes)
            {
                return;
            }

            string oldestArchive = $"{LogFile}.{MaxArchiveFiles}";
            if (File.Exists(oldestArchive))
            {
                File.Delete(oldestArchive);
            }

            for (int i = MaxArchiveFiles - 1; i >= 1; i--)
            {
                string source = $"{LogFile}.{i}";
                if (File.Exists(source))
                {
                    File.Move(source, $"{LogFile}.{i + 1}", true);
                }
            }

            File.Move(LogFile, $"{LogFile}.1", true);
        }
        catch
        {
            // Se a rotação falhar, não impedimos o fluxo principal do app.
        }
    }
}
