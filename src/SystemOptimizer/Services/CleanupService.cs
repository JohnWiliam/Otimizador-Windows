using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceProcess;
using System.Threading.Tasks;
using SystemOptimizer.Helpers;
using SystemOptimizer.Models;
using SystemOptimizer.Properties; // Namespace dos Resources

namespace SystemOptimizer.Services;

public class CleanupService
{
    public event Action<CleanupLogItem>? OnLogItem;

    public async Task RunCleanupAsync()
    {
        await Task.Run(async () =>
        {
            OnLogItem?.Invoke(new CleanupLogItem { Message = Resources.Log_Starting, Icon = "Play24", StatusColor = "#0078D4", IsBold = true });

            // 1. Windows Update Cleanup (Smart com Retry)
            await CleanWindowsUpdateAsync();

            // 2. File Cleanup Genérico
            var paths = new Dictionary<string, string>
            {
                { "Arquivos Temp", Path.GetTempPath() },
                { "Temp Sistema", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp") },
                { "Prefetch", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch") },
                { "Shader Cache (DX)", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NVIDIA", "DXCache") },
                { "Shader Cache (D3D)", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3DSCache") },
                { "Relatórios de Erro (WER)", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "WER") },
                { "CrashDumps", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps") },
                { "Windows Logs", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Logs") }
            };

            long totalBytes = 0;

            foreach (var kvp in paths)
            {
                if (Directory.Exists(kvp.Value))
                {
                    totalBytes += CleanDirectory(kvp.Value, kvp.Key);
                }
            }

            // 3. Limpeza Especial: Chrome (Suporte a múltiplos perfis)
            string chromeUserData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "User Data");
            if (Directory.Exists(chromeUserData))
            {
                try
                {
                    foreach (var dir in Directory.GetDirectories(chromeUserData))
                    {
                        // Validação Extra: Verifica se parece um perfil real para evitar deletar pastas de sistema do Chrome
                        if (File.Exists(Path.Combine(dir, "Preferences")) || dir.EndsWith("Default") || dir.Contains("Profile"))
                        {
                            string cachePath = Path.Combine(dir, "Cache", "Cache_Data");
                            if (Directory.Exists(cachePath))
                            {
                                string profileName = new DirectoryInfo(dir).Name;
                                totalBytes += CleanDirectory(cachePath, $"Chrome Cache ({profileName})");
                            }
                        }
                    }
                }
                catch { }
            }

            // 4. DNS Cache
            try
            {
                CommandHelper.RunCommand("powershell", "Clear-DnsClientCache");
                OnLogItem?.Invoke(new CleanupLogItem { Message = Resources.Log_DNS, Icon = "Globe24", StatusColor = "Green" });
            }
            catch { }

            double totalMb = Math.Round(totalBytes / 1024.0 / 1024.0, 2);
            OnLogItem?.Invoke(new CleanupLogItem
            {
                Message = string.Format(Resources.Log_Finished, totalMb),
                Icon = "Delete24",
                StatusColor = "#0078D4",
                IsBold = true
            });
        });
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
            OnLogItem?.Invoke(new CleanupLogItem { Message = string.Format(Resources.Log_Clean, label), Icon = "Info24", StatusColor = "Gray" });
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

            // CORREÇÃO: Retry Pattern ao invés de Delay fixo
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
                    await Task.Delay(500); // Espera 500ms entre tentativas
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
