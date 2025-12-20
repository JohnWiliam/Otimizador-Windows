using System.Diagnostics;
using System.Text;
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
                    CreateNoWindow = true,
                    // Garante que caracteres especiais (acentos) sejam lidos corretamente
                    StandardOutputEncoding = Encoding.UTF8, 
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = Process.Start(psi);
                if (process == null) return string.Empty;
                
                // Leitura assíncrona para evitar Deadlocks
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                
                // Aguarda ambas as streams e a saída do processo
                Task.WaitAll(outputTask, errorTask);
                process.WaitForExit();
                
                string output = outputTask.Result;
                string error = errorTask.Result;

                // Se houver erro na stream de erro, mas o output estiver vazio, retorna o erro.
                // Alguns comandos (como netsh) as vezes escrevem avisos no stderr que não são erros fatais.
                if (!string.IsNullOrWhiteSpace(error) && string.IsNullOrWhiteSpace(output) && process.ExitCode != 0)
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
