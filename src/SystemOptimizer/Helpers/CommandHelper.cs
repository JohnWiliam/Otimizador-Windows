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
                
                // Leitura dos streams antes do WaitForExit para evitar deadlocks
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                
                process.WaitForExit();
                
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
