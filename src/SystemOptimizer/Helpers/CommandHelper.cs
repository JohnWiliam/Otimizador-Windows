using System.Diagnostics;
using System.Threading.Tasks;

namespace SystemOptimizer.Helpers
{
    public static class CommandHelper
    {
        public static string RunCommand(string fileName, string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return string.Empty;
                
                // Correção de Deadlock: Ler streams de forma assíncrona para evitar bloqueio do buffer
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                
                // Aguarda ambas as leituras completarem
                Task.WaitAll(outputTask, errorTask);
                
                process.WaitForExit();
                
                string output = outputTask.Result;
                string error = errorTask.Result;
                
                // Se houver erro crítico e nenhum output útil, retorna o erro para fins de debug
                if (!string.IsNullOrEmpty(error) && string.IsNullOrWhiteSpace(output))
                {
                    return $"[ERRO CMD] {error}";
                }

                return output;
            }
            catch
            {
                return string.Empty;
            }
        }
        
        public static void RunCommandNoWait(string fileName, string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = true, 
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };
                Process.Start(psi);
            }
            catch { }
        }
    }
}
