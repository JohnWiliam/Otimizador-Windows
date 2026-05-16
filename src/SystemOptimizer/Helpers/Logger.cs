using System;
using System.IO;
using System.Text;

namespace SystemOptimizer.Helpers;

public static class Logger
{
    private const long MaxLogBytes = 2 * 1024 * 1024;
    private const int RetainedLogFiles = 3;
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
                RotateIfNeeded(Encoding.UTF8.GetByteCount(logEntry));
                File.AppendAllText(LogFile, logEntry, Encoding.UTF8);
            }
        }
        catch
        {
            // Ignora erros de gravação de log (ex: arquivo em uso)
        }
    }

    private static void RotateIfNeeded(int incomingBytes)
    {
        var logInfo = new FileInfo(LogFile);
        if (!logInfo.Exists || logInfo.Length + incomingBytes <= MaxLogBytes)
        {
            return;
        }

        for (int i = RetainedLogFiles - 1; i >= 1; i--)
        {
            string source = $"{LogFile}.{i}";
            string destination = $"{LogFile}.{i + 1}";
            if (File.Exists(source))
            {
                File.Copy(source, destination, true);
                File.Delete(source);
            }
        }

        File.Copy(LogFile, $"{LogFile}.1", true);
        File.WriteAllText(LogFile, string.Empty, Encoding.UTF8);
    }
}
