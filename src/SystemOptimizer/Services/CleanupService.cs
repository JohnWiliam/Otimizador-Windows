using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading.Tasks;
using SystemOptimizer.Helpers;
using SystemOptimizer.Models;
using SystemOptimizer.Properties; 

namespace SystemOptimizer.Services;

public class CleanupService
{
    public event Action<CleanupLogItem>? OnLogItem;

    [DllImport("shell32.dll")]
    static extern int SHEmptyRecycleBin(IntPtr hwnd, string? rootPath, uint dwFlags);

    const uint SHERB_NOCONFIRMATION = 0x00000001;
    const uint SHERB_NOPROGRESSUI = 0x00000002;
    const uint SHERB_NOSOUND = 0x00000004;

    public async Task RunCleanupAsync()
    {
        await RunCleanupAsync(new CleanupOptions 
        { 
            CleanUserTemp = true, 
            CleanSystemTemp = true,
            CleanPrefetch = true,
            CleanBrowserCache = true,
            CleanDns = true,
            CleanWindowsUpdate = true,
            CleanRecycleBin = false 
        });
    }

    public async Task RunCleanupAsync(CleanupOptions options)
    {
        await Task.Run(async () =>
        {
            OnLogItem?.Invoke(new CleanupLogItem { Message = Resources.Log_Starting, Icon = "Play24", StatusColor = "#0078D4", IsBold = true });
            long totalBytes = 0;

            // 1. Windows Update
            if (options.CleanWindowsUpdate)
            {
                await CleanWindowsUpdateAsync();
            }

            // 2. Limpeza de Arquivos Genéricos (Temp Usuário)
            // Removemos Shaders daqui para agrupar depois
            if (options.CleanUserTemp)
            {
                var userPaths = new Dictionary<string, string>
                {
                    { Resources.Label_TempFiles ?? "User Temp", Path.GetTempPath() },
                    { "WER Reports", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "WER") },
                    { "CrashDumps", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps") }
                };

                foreach (var kvp in userPaths)
                {
                    if (Directory.Exists(kvp.Value))
                        totalBytes += CleanDirectory(kvp.Value, kvp.Key, true).Bytes;
                }

                // --- Shader Cache Unificado (Nvidia, AMD, DX) ---
                long shaderBytes = 0;
                int shaderSkipped = 0;
                var shaderPaths = new List<string>
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NVIDIA", "DXCache"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3DSCache"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AMD", "DxCache")
                };

                foreach (var path in shaderPaths)
                {
                    if (Directory.Exists(path))
                    {
                        var res = CleanDirectory(path, null, false); // False para não gerar log individual
                        shaderBytes += res.Bytes;
                        shaderSkipped += res.Skipped;
                    }
                }
                
                if (shaderBytes > 0 || shaderSkipped > 0)
                {
                    totalBytes += shaderBytes;
                    LogAggregateResult(Resources.Label_ShaderCache ?? "Shader Cache", shaderBytes, shaderSkipped);
                }
            }

            // 3. Temp Sistema
            if (options.CleanSystemTemp)
            {
                var sysPaths = new Dictionary<string, string>
                {
                    { Resources.Label_SystemTemp ?? "System Temp", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp") },
                    { "Windows Logs", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Logs") }
                };

                foreach (var kvp in sysPaths)
                {
                    if (Directory.Exists(kvp.Value))
                        totalBytes += CleanDirectory(kvp.Value, kvp.Key, true).Bytes;
                }
            }

            // 4. Prefetch
            if (options.CleanPrefetch)
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch");
                if (Directory.Exists(path))
                    totalBytes += CleanDirectory(path, "Prefetch", true).Bytes;
            }

            // 5. Navegadores Unificado
            if (options.CleanBrowserCache)
            {
                totalBytes += CleanBrowsersUnified();
            }

            // 6. DNS
            if (options.CleanDns)
            {
                try
                {
                    CommandHelper.RunCommand("powershell", "Clear-DnsClientCache");
                    OnLogItem?.Invoke(new CleanupLogItem { Message = Resources.Log_DNSCleared, Icon = "Globe24", StatusColor = "Green" });
                }
                catch { }
            }

            // 7. Lixeira
            if (options.CleanRecycleBin)
            {
                try
                {
                    SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
                    OnLogItem?.Invoke(new CleanupLogItem { Message = Resources.Log_RecycleBinEmptied, Icon = "Delete24", StatusColor = "Green" });
                }
                catch { }
            }

            double totalMb = Math.Round(totalBytes / 1024.0 / 1024.0, 2);
            OnLogItem?.Invoke(new CleanupLogItem
            {
                Message = string.Format(Resources.Log_Finished, totalMb),
                Icon = "CheckmarkCircle24",
                StatusColor = "#0078D4",
                IsBold = true
            });
        });
    }

    private long CleanBrowsersUnified()
    {
        long totalBytes = 0;
        int totalSkipped = 0;
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // -- Chrome --
        string chromePath = Path.Combine(localAppData, "Google", "Chrome", "User Data");
        var chromeRes = CleanChromiumBrowser(chromePath, false);
        totalBytes += chromeRes.Bytes;
        totalSkipped += chromeRes.Skipped;

        // -- Edge --
        string edgePath = Path.Combine(localAppData, "Microsoft", "Edge", "User Data");
        var edgeRes = CleanChromiumBrowser(edgePath, false);
        totalBytes += edgeRes.Bytes;
        totalSkipped += edgeRes.Skipped;

        // -- Firefox --
        string firefoxPath = Path.Combine(localAppData, "Mozilla", "Firefox", "Profiles");
        if (Directory.Exists(firefoxPath))
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(firefoxPath))
                {
                    string cachePath = Path.Combine(dir, "cache2", "entries");
                    if (Directory.Exists(cachePath))
                    {
                        var res = CleanDirectory(cachePath, null, false);
                        totalBytes += res.Bytes;
                        totalSkipped += res.Skipped;
                    }
                }
            }
            catch { }
        }

        // Log Unificado
        LogAggregateResult(Resources.Label_BrowserCache ?? "Browser Cache", totalBytes, totalSkipped);
        
        return totalBytes;
    }

    private (long Bytes, int Skipped) CleanChromiumBrowser(string userDataPath, bool logOutput)
    {
        long bytes = 0;
        int skipped = 0;

        if (Directory.Exists(userDataPath))
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(userDataPath))
                {
                    if (File.Exists(Path.Combine(dir, "Preferences")) || dir.EndsWith("Default") || dir.Contains("Profile"))
                    {
                        string cachePath = Path.Combine(dir, "Cache", "Cache_Data");
                        if (Directory.Exists(cachePath))
                        {
                            var res = CleanDirectory(cachePath, null, logOutput);
                            bytes += res.Bytes;
                            skipped += res.Skipped;
                        }
                    }
                }
            }
            catch { }
        }
        return (bytes, skipped);
    }

    // Método auxiliar para formatar e enviar log unificado
    private void LogAggregateResult(string label, long bytes, int skipped)
    {
        if (bytes > 0)
        {
            double mb = Math.Round(bytes / 1024.0 / 1024.0, 2);
            string msg = string.Format(Resources.Log_Removed, label, mb);
            
            if (skipped > 0)
                msg += string.Format(Resources.Log_Ignored, skipped);

            OnLogItem?.Invoke(new CleanupLogItem
            {
                Message = msg,
                Icon = "Checkmark24",
                StatusColor = "Green"
            });
        }
        else
        {
            string msg = string.Format(Resources.Log_Clean ?? "{0} Clean", label);
            OnLogItem?.Invoke(new CleanupLogItem { Message = msg, Icon = "Info24", StatusColor = "Gray" });
        }
    }

    private (long Bytes, int Skipped) CleanDirectory(string path, string? label, bool logOutput)
    {
        long categoryBytes = 0;
        int skippedCount = 0;
        var dirInfo = new DirectoryInfo(path);

        try {
            foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                try
                {
                    long size = file.Length;
                    file.Delete();
                    if (!file.Exists) categoryBytes += size;
                }
                catch { skippedCount++; }
            }
        } catch { }

        try {
            foreach (var dir in dirInfo.EnumerateDirectories("*", SearchOption.AllDirectories))
            {
                try { dir.Delete(true); } catch { }
            }
        } catch { }

        if (logOutput && label != null)
        {
            LogAggregateResult(label, categoryBytes, skippedCount);
        }

        return (categoryBytes, skippedCount);
    }

    private async Task CleanWindowsUpdateAsync()
    {
        string wuPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download");
        if (!Directory.Exists(wuPath)) return;

        string[] services = ["wuauserv", "bits", "cryptsvc"];
        bool stopped = await ToggleServicesAsync(services, false);

        if (stopped)
        {
            OnLogItem?.Invoke(new CleanupLogItem { Message = Resources.Log_WUServicesStopped, Icon = "Pause24", StatusColor = "#CA5010" });

            bool success = false;
            int attempts = 0;
            while (!success && attempts < 5)
            {
                try
                {
                    CleanDirectory(wuPath, "Windows Update", true);
                    success = true;
                }
                catch
                {
                    attempts++;
                    await Task.Delay(500); 
                }
            }

            if (!success)
            {
                OnLogItem?.Invoke(new CleanupLogItem { Message = Resources.Log_WUError, Icon = "Warning24", StatusColor = "Orange" });
            }

            await ToggleServicesAsync(services, true);
            OnLogItem?.Invoke(new CleanupLogItem { Message = Resources.Log_WUServicesRestarted, Icon = "Play24", StatusColor = "Green" });
        }
        else
        {
            OnLogItem?.Invoke(new CleanupLogItem { Message = Resources.Log_WUFailStop, Icon = "Warning24", StatusColor = "Orange" });
        }
    }

    private static async Task<bool> ToggleServicesAsync(string[] services, bool start)
    {
        return await Task.Run(() =>
        {
            try
            {
                foreach (var s in services)
                {
                    using var sc = new ServiceController(s);
                    if (start)
                    {
                        if (sc.Status != ServiceControllerStatus.Running)
                        {
                            sc.Start();
                            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                        }
                    }
                    else
                    {
                        if (sc.Status != ServiceControllerStatus.Stopped)
                        {
                            sc.Stop();
                            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                        }
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        });
    }
}

public class CleanupOptions
{
    public bool CleanUserTemp { get; set; } = true;
    public bool CleanSystemTemp { get; set; } = true;
    public bool CleanPrefetch { get; set; } = true;
    public bool CleanBrowserCache { get; set; } = true;
    public bool CleanDns { get; set; } = true;
    public bool CleanWindowsUpdate { get; set; } = true;
    public bool CleanRecycleBin { get; set; } = false;
}
