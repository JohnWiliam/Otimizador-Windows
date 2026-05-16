using System;
using System.IO;
using System.Text;

namespace SystemOptimizer.Helpers;

public static class Logger
{
    // Define o caminho fixo: C:\ProgramData\SystemOptimizer\system_optimizer_log.txt
    private static readonly string LogFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), 
        "SystemOptimizer");
    
    private static readonly string LogFile = Path.Combine(LogFolder, "system_optimizer_log.txt");
    private static readonly object SyncRoot = new();
    private const long MaxLogBytes = 1_048_576;
    private const int MaxArchiveFiles = 3;

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
                RotateIfNeeded(logEntry);
                File.AppendAllText(LogFile, logEntry, Encoding.UTF8);
            }
        }
        catch
        {
            // Ignora erros de gravação de log (ex: arquivo em uso)
        }
    }

    private static void RotateIfNeeded(string nextEntry)
    {
        var logInfo = new FileInfo(LogFile);
        if (!logInfo.Exists || logInfo.Length + Encoding.UTF8.GetByteCount(nextEntry) <= MaxLogBytes)
        {
            return;
        }

        for (int i = MaxArchiveFiles - 1; i >= 1; i--)
        {
            string source = $"{LogFile}.{i}";
            string destination = $"{LogFile}.{i + 1}";
            if (File.Exists(source))
            {
                File.Copy(source, destination, overwrite: true);
            }
        }

        File.Copy(LogFile, $"{LogFile}.1", overwrite: true);
        File.WriteAllText(LogFile, string.Empty, Encoding.UTF8);
    }
}
