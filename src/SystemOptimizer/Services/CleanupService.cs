using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceProcess;
using System.Threading.Tasks;
using SystemOptimizer.Models;

namespace SystemOptimizer.Services
{
    public class CleanupService
    {
        public event Action<CleanupLogItem>? OnLogItem;

        public async Task RunCleanupAsync()
        {
            await Task.Run(async () =>
            {
                OnLogItem?.Invoke(new CleanupLogItem { Message = "Iniciando varredura e limpeza...", Icon = "Play24", StatusColor = "#0078D4", IsBold = true });

                // 1. Windows Update Cleanup (Smart)
                await CleanWindowsUpdateAsync();

                // 2. File Cleanup
                var paths = new Dictionary<string, string>
                {
                    { "Arquivos Temp", Path.GetTempPath() },
                    { "Temp Sistema", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp") },
                    { "Prefetch", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch") },
                    { "Shader Cache (DX)", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NVIDIA", "DXCache") },
                    { "Shader Cache (D3D)", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3DSCache") },
                    { "Relatórios de Erro (WER)", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "WER") },
                    { "CrashDumps", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps") },
                    { "Windows Logs", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Logs") },
                    { "Cache Chrome", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "User Data", "Default", "Cache", "Cache_Data") }
                };

                long totalBytes = 0;

                foreach (var kvp in paths)
                {
                    if (Directory.Exists(kvp.Value))
                    {
                        long categoryBytes = 0;
                        int skippedCount = 0;

                        try
                        {
                            var dirInfo = new DirectoryInfo(kvp.Value);

                            try {
                                foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                                {
                                    try
                                    {
                                        long size = file.Length;
                                        file.Delete();
                                        if (!file.Exists)
                                        {
                                            categoryBytes += size;
                                        }
                                    }
                                    catch
                                    {
                                        skippedCount++;
                                    }
                                }
                            } catch { }

                            try {
                                foreach (var dir in dirInfo.EnumerateDirectories("*", SearchOption.AllDirectories))
                                {
                                    try { dir.Delete(true); } catch { }
                                }
                            } catch { }
                        }
                        catch { }

                        if (categoryBytes > 0)
                        {
                            double mb = Math.Round(categoryBytes / 1024.0 / 1024.0, 2);
                            OnLogItem?.Invoke(new CleanupLogItem
                            {
                                Message = $"{kvp.Key} : {mb} MB removidos" + (skippedCount > 0 ? $" ({skippedCount} ignorados)" : ""),
                                Icon = "Checkmark24",
                                StatusColor = "Green"
                            });
                            totalBytes += categoryBytes;
                        }
                        else
                        {
                            OnLogItem?.Invoke(new CleanupLogItem
                            {
                                Message = $"{kvp.Key} : Limpo (ou ignorado)",
                                Icon = "Info24",
                                StatusColor = "Gray"
                            });
                        }
                    }
                }

                // 3. DNS Cache
                try
                {
                    Helpers.CommandHelper.RunCommand("powershell", "Clear-DnsClientCache");
                    OnLogItem?.Invoke(new CleanupLogItem { Message = "Cache DNS: Limpo", Icon = "Globe24", StatusColor = "Green" });
                }
                catch { }

                double totalMb = Math.Round(totalBytes / 1024.0 / 1024.0, 2);
                OnLogItem?.Invoke(new CleanupLogItem
                {
                    Message = $"LIMPEZA CONCLUÍDA! Liberado: {totalMb} MB",
                    Icon = "Delete24",
                    StatusColor = "#0078D4",
                    IsBold = true
                });
            });
        }

        private async Task CleanWindowsUpdateAsync()
        {
            string wuPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download");
            if (!Directory.Exists(wuPath)) return;

            string[] services = { "wuauserv", "bits", "cryptsvc" };
            bool stopped = await ToggleServicesAsync(services, false);

            if (stopped)
            {
                OnLogItem?.Invoke(new CleanupLogItem { Message = "Serviços Windows Update: Parados para limpeza.", Icon = "Pause24", StatusColor = "#CA5010" });
                
                // Clean
                try
                {
                    long bytes = 0;
                    var dir = new DirectoryInfo(wuPath);
                    foreach (var file in dir.EnumerateFiles("*", SearchOption.AllDirectories))
                    {
                        try { 
                            long s = file.Length; 
                            file.Delete(); 
                            bytes += s; 
                        } catch { }
                    }
                    foreach (var d in dir.EnumerateDirectories("*", SearchOption.AllDirectories))
                    {
                        try { d.Delete(true); } catch { }
                    }
                    
                    double mb = Math.Round(bytes / 1024.0 / 1024.0, 2);
                    OnLogItem?.Invoke(new CleanupLogItem { Message = $"Windows Update : {mb} MB removidos", Icon = "Checkmark24", StatusColor = "Green" });
                }
                catch
                {
                    OnLogItem?.Invoke(new CleanupLogItem { Message = "Windows Update : Falha ao acessar arquivos.", Icon = "ErrorCircle24", StatusColor = "Red" });
                }

                await ToggleServicesAsync(services, true);
                OnLogItem?.Invoke(new CleanupLogItem { Message = "Serviços Windows Update: Reiniciados.", Icon = "Play24", StatusColor = "Green" });
            }
            else
            {
                OnLogItem?.Invoke(new CleanupLogItem { Message = "Windows Update : Não foi possível parar serviços. Pulando.", Icon = "Warning24", StatusColor = "Orange" });
            }
        }

        private async Task<bool> ToggleServicesAsync(string[] services, bool start)
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
}
