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
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return string.Empty;
                
                // Read output BEFORE waiting for exit to avoid deadlock on full buffer
                string output = process.StandardOutput?.ReadToEnd() ?? string.Empty;
                process.WaitForExit();
                
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
