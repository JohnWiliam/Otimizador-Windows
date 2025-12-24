using System;
using System.IO;

namespace SystemOptimizer.Helpers;

public static class Logger
{
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

            // Adiciona o texto ao final do arquivo (Append)
            File.AppendAllText(LogFile, logEntry);
        }
        catch
        {
            // Ignora erros de gravação de log (ex: arquivo em uso)
        }
    }
}
