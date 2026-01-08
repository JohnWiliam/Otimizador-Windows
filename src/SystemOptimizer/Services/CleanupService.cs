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

    // P/Invoke para Lixeira
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

            // 1. Windows Update Cleanup
            if (options.CleanWindowsUpdate)
            {
                await CleanWindowsUpdateAsync();
            }

            // 2. File Cleanup Genérico
            var pathsToClean = new Dictionary<string, string>();

            if (options.CleanUserTemp)
            {
                // Usa Resources para o label (PT: Arquivos Temporários / EN: Temporary Files)
                pathsToClean.Add(Resources.Label_TempFiles ?? "User Temp", Path.GetTempPath());
                pathsToClean.Add("Shader Cache (DX)", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NVIDIA", "DXCache"));
                pathsToClean.Add("Shader Cache (D3D)", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3DSCache"));
                pathsToClean.Add("WER Reports", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "WER"));
                pathsToClean.Add("CrashDumps", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps"));
            }

            if (options.CleanSystemTemp)
            {
                pathsToClean.Add(Resources.Label_SystemTemp ?? "System Temp", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"));
                pathsToClean.Add("Windows Logs", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Logs"));
            }

            if (options.CleanPrefetch)
            {
                pathsToClean.Add("Prefetch", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch"));
            }

            foreach (var kvp in pathsToClean)
            {
                if (Directory.Exists(kvp.Value))
                {
                    totalBytes += CleanDirectory(kvp.Value, kvp.Key);
                }
            }

            // 3. Limpeza de Navegadores
            if (options.CleanBrowserCache)
            {
                totalBytes += CleanBrowsers();
            }

            // 4. DNS Cache
            if (options.CleanDns)
            {
                try
                {
                    CommandHelper.RunCommand("powershell", "Clear-DnsClientCache");
                    string msg = Resources.Log_DNSCleared ?? "DNS cache cleared";
                    OnLogItem?.Invoke(new CleanupLogItem { Message = msg, Icon = "Globe24", StatusColor = "Green" });
                }
                catch { }
            }

            // 5. Lixeira
            if (options.CleanRecycleBin)
            {
                try
                {
                    SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
                    string msg = Resources.Log_RecycleBinEmptied ?? "Recycle Bin emptied";
                    OnLogItem?.Invoke(new CleanupLogItem { Message = msg, Icon = "Delete24", StatusColor = "Green" });
                }
                catch
                {
                     // Ignora erros
                }
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

    private long CleanBrowsers()
    {
        long bytesCleaned = 0;
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // -- Chrome --
        string chromePath = Path.Combine(localAppData, "Google", "Chrome", "User Data");
        bytesCleaned += CleanChromiumBrowser(chromePath, "Chrome");

        // -- Edge --
        string edgePath = Path.Combine(localAppData, "Microsoft", "Edge", "User Data");
        bytesCleaned += CleanChromiumBrowser(edgePath, "Edge");

        // -- Firefox --
        string firefoxPath = Path.Combine(localAppData, "Mozilla", "Firefox", "Profiles");
        if (Directory.Exists(firefoxPath))
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(firefoxPath))
                {
                    // Firefox usa pasta "cache2" dentro do perfil
                    string cachePath = Path.Combine(dir, "cache2", "entries");
                    if (Directory.Exists(cachePath))
                    {
                        string dirName = new DirectoryInfo(dir).Name;
                        string label = $"Firefox Cache ({dirName})";
                        bytesCleaned += CleanDirectory(cachePath, label);
                    }
                }
            }
            catch { }
        }

        return bytesCleaned;
    }

    private long CleanChromiumBrowser(string userDataPath, string browserName)
    {
        long bytes = 0;
        if (Directory.Exists(userDataPath))
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(userDataPath))
                {
                    // Identifica pastas de perfil
                    if (File.Exists(Path.Combine(dir, "Preferences")) || dir.EndsWith("Default") || dir.Contains("Profile"))
                    {
                        // Limpa apenas Cache_Data, preservando cookies/histórico
                        string cachePath = Path.Combine(dir, "Cache", "Cache_Data");
                        if (Directory.Exists(cachePath))
                        {
                            string profileName = new DirectoryInfo(dir).Name;
                            string label = $"{browserName} Cache ({profileName})";
                            bytes += CleanDirectory(cachePath, label);
                        }
                    }
                }
            }
            catch { }
        }
        return bytes;
    }

    private long CleanDirectory(string path, string label)
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

        if (categoryBytes > 0)
        {
            double mb = Math.Round(categoryBytes / 1024.0 / 1024.0, 2);
            string msg = string.Format(Resources.Log_Removed, label, mb);
            if (skippedCount > 0) msg += $" ({skippedCount} ignored)";

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
        return categoryBytes;
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
                    CleanDirectory(wuPath, "Windows Update");
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
