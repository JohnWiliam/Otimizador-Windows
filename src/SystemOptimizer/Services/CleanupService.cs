using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading.Tasks;
using SystemOptimizer.Models;
using SystemOptimizer.Properties;

namespace SystemOptimizer.Services;

public class CleanupService
{
    private readonly CleanupExecutionEngine _executionEngine = new();

    public event Action<CleanupLogItem>? OnLogItem;

    private sealed class CleanupResult
    {
        public long EstimatedBytes { get; private set; }
        public long RemovedBytes { get; private set; }
        public int FilesFound { get; private set; }
        public int FilesRemoved { get; private set; }
        public int AccessDeniedErrors { get; private set; }
        public int FileInUseErrors { get; private set; }
        public int OtherErrors { get; private set; }

        public int TotalErrors => AccessDeniedErrors + FileInUseErrors + OtherErrors;
        public double SuccessRate => FilesFound == 0 ? 100 : Math.Round((double)FilesRemoved / FilesFound * 100, 1);

        public void RegisterFileFound(long fileSize)
        {
            FilesFound++;
            EstimatedBytes += fileSize;
        }

        public void RegisterFileRemoved(long fileSize)
        {
            FilesRemoved++;
            RemovedBytes += fileSize;
        }

        public void RegisterAccessDeniedError() => AccessDeniedErrors++;
        public void RegisterFileInUseError() => FileInUseErrors++;
        public void RegisterOtherError() => OtherErrors++;

        public void Merge(CleanupResult other)
        {
            EstimatedBytes += other.EstimatedBytes;
            RemovedBytes += other.RemovedBytes;
            FilesFound += other.FilesFound;
            FilesRemoved += other.FilesRemoved;
            AccessDeniedErrors += other.AccessDeniedErrors;
            FileInUseErrors += other.FileInUseErrors;
            OtherErrors += other.OtherErrors;
        }

        public static CleanupResult FailedOperation() => new() { OtherErrors = 1 };
        public static CleanupResult SuccessfulOperation() => new();
    }

    private static void MergeTargetResult(Dictionary<string, CleanupResult> targetResults, string target, CleanupResult result)
    {
        if (!targetResults.TryGetValue(target, out var existing))
        {
            targetResults[target] = result;
            return;
        }

        existing.Merge(result);
    }

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
            var targetResults = new Dictionary<string, CleanupResult>(StringComparer.OrdinalIgnoreCase);

            // 1. Windows Update
            if (options.CleanWindowsUpdate)
            {
                var wuResult = await CleanWindowsUpdateAsync();
                MergeTargetResult(targetResults, "Windows Update", wuResult);
            }

            // 2. Arquivos Temporários do Usuário (Temp + CrashDumps + WER)
            if (options.CleanUserTemp)
            {
                // Limpeza de pastas gerais
                var userPaths = new Dictionary<string, string>
                {
                    { Resources.Label_TempFiles ?? "User Temp", Path.GetTempPath() },
                    { "WER Reports", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "WER") },
                    { "CrashDumps", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps") }
                };

                foreach (var kvp in userPaths)
                {
                    if (Directory.Exists(kvp.Value))
                    {
                        // Log individual para pastas importantes do sistema
                        var res = CleanDirectory(kvp.Value, null, false);
                        MergeTargetResult(targetResults, kvp.Key, res);
                        if (res.RemovedBytes > 0 || kvp.Key == (Resources.Label_TempFiles ?? "User Temp"))
                            LogAggregateResult(kvp.Key, res);
                    }
                }

                // --- Shader Cache Unificado (Nvidia, AMD, DX) ---
                var shaderResult = new CleanupResult();
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
                        var res = CleanDirectory(path, null, false);
                        shaderResult.Merge(res);
                    }
                }
                
                // Emite UM log para todos os Shaders
                MergeTargetResult(targetResults, Resources.Label_ShaderCache ?? "Shader Cache", shaderResult);
                LogAggregateResult(Resources.Label_ShaderCache ?? "Shader Cache", shaderResult);
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
                    {
                        var res = CleanDirectory(kvp.Value, kvp.Key, false);
                        MergeTargetResult(targetResults, kvp.Key, res);
                        LogAggregateResult(kvp.Key, res);
                    }
                }
            }

            // 4. Prefetch
            if (options.CleanPrefetch)
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch");
                if (Directory.Exists(path))
                {
                    var res = CleanDirectory(path, "Prefetch", false);
                    MergeTargetResult(targetResults, "Prefetch", res);
                    LogAggregateResult("Prefetch", res);
                }
            }

            // 5. Navegadores Unificado
            if (options.CleanBrowserCache)
            {
                var browserRes = CleanBrowsersUnified();
                MergeTargetResult(targetResults, Resources.Label_BrowserCache ?? "Browser Cache", browserRes);
            }

            // 6. DNS
            if (options.CleanDns)
            {
                try
                {
                    CommandHelper.RunCommand("powershell", "Clear-DnsClientCache");
                    OnLogItem?.Invoke(new CleanupLogItem { Message = Resources.Log_DNSCleared, Icon = "Globe24", StatusColor = "Green" });
                    MergeTargetResult(targetResults, "DNS", CleanupResult.SuccessfulOperation());
                }
                catch (Exception ex)
                {
                    MergeTargetResult(targetResults, "DNS", CleanupResult.FailedOperation());
                    Logger.Log($"Falha ao limpar cache DNS: {ex.Message}", "ERROR");
                    OnLogItem?.Invoke(new CleanupLogItem { Message = $"DNS: falha ao limpar cache ({ex.Message})", Icon = "Warning24", StatusColor = "Orange" });
                }
            }

            // 7. Lixeira
            if (options.CleanRecycleBin)
            {
                try
                {
                    SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
                    OnLogItem?.Invoke(new CleanupLogItem { Message = Resources.Log_RecycleBinEmptied, Icon = "Delete24", StatusColor = "Green" });
                    MergeTargetResult(targetResults, "Recycle Bin", CleanupResult.SuccessfulOperation());
                }
                catch (Exception ex)
                {
                    MergeTargetResult(targetResults, "Recycle Bin", CleanupResult.FailedOperation());
                    Logger.Log($"Falha ao esvaziar lixeira: {ex.Message}", "ERROR");
                    OnLogItem?.Invoke(new CleanupLogItem { Message = $"Lixeira: falha ao esvaziar ({ex.Message})", Icon = "Warning24", StatusColor = "Orange" });
                }
            }

            long totalEstimatedBytes = targetResults.Values.Sum(r => r.EstimatedBytes);
            long totalRemovedBytes = targetResults.Values.Sum(r => r.RemovedBytes);
            int totalFilesFound = targetResults.Values.Sum(r => r.FilesFound);
            int totalFilesRemoved = targetResults.Values.Sum(r => r.FilesRemoved);
            int totalFailures = targetResults.Values.Sum(r => r.TotalErrors);
            double totalSuccessRate = totalFilesFound == 0 ? 100 : Math.Round((double)totalFilesRemoved / totalFilesFound * 100, 1);
            double totalMb = Math.Round(totalRemovedBytes / 1024.0 / 1024.0, 2);

            OnLogItem?.Invoke(new CleanupLogItem
            {
                Message = string.Format(Resources.Log_Finished, totalMb, totalFilesRemoved, totalFilesFound, totalSuccessRate),
                Icon = "CheckmarkCircle24",
                StatusColor = "#0078D4",
                IsBold = true
            });

            OnLogItem?.Invoke(new CleanupLogItem
            {
                Message = string.Format(Resources.Log_Summary, totalFilesRemoved, totalFilesFound, totalSuccessRate, totalFailures, Math.Round(totalEstimatedBytes / 1024.0 / 1024.0, 2)),
                Icon = "Info24",
                StatusColor = totalFailures > 0 ? "Orange" : "Green"
            });
        });
    }

    private CleanupResult CleanBrowsersUnified()
    {
        var totalResult = new CleanupResult();
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // -- Chrome --
        string chromePath = Path.Combine(localAppData, "Google", "Chrome", "User Data");
        var chromeRes = CleanChromiumBrowser(chromePath, false);
        totalResult.Merge(chromeRes);

        // -- Edge --
        string edgePath = Path.Combine(localAppData, "Microsoft", "Edge", "User Data");
        var edgeRes = CleanChromiumBrowser(edgePath, false);
        totalResult.Merge(edgeRes);

        // -- Firefox --
        string firefoxPath = Path.Combine(localAppData, "Mozilla", "Firefox", "Profiles");
        if (Directory.Exists(firefoxPath))
        {
            foreach (var target in provider.GetTargets())
            {
                foreach (var dir in Directory.GetDirectories(firefoxPath))
                {
                    string cachePath = Path.Combine(dir, "cache2", "entries");
                    if (Directory.Exists(cachePath))
                    {
                        var res = CleanDirectory(cachePath, null, false);
                        totalResult.Merge(res);
                    }
                }
            }
            catch (Exception ex)
            {
                totalResult.RegisterOtherError();
                Logger.Log($"Falha ao limpar cache do Firefox: {ex.Message}", "ERROR");
                OnLogItem?.Invoke(new CleanupLogItem { Message = $"Navegador (Firefox): erro ao limpar cache ({ex.Message})", Icon = "Warning24", StatusColor = "Orange" });
            }
        }

        // Log Unificado para todos os navegadores
        LogAggregateResult(Resources.Label_BrowserCache ?? "Browser Cache", totalResult);
        
        if (totalResult.TotalErrors > 0)
        {
            OnLogItem?.Invoke(new CleanupLogItem { Message = $"Navegadores: {totalResult.TotalErrors} falha(s) durante a limpeza.", Icon = "Warning24", StatusColor = "Orange" });
        }

        return totalResult;
    }

    private CleanupResult CleanChromiumBrowser(string userDataPath, bool logOutput)
    {
        var result = new CleanupResult();

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
                            result.Merge(res);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.RegisterOtherError();
                Logger.Log($"Falha ao limpar cache Chromium em '{userDataPath}': {ex.Message}", "ERROR");
                OnLogItem?.Invoke(new CleanupLogItem { Message = $"Navegador: erro ao acessar perfil ({ex.Message})", Icon = "Warning24", StatusColor = "Orange" });
            }
        }
        return result;
    }

    private void LogAggregateResult(string label, CleanupResult result)
    {
        if (result.FilesFound > 0 || result.RemovedBytes > 0)
        {
            double removedMb = Math.Round(result.RemovedBytes / 1024.0 / 1024.0, 2);
            string msg = string.Format(Resources.Log_Removed, label, removedMb, result.FilesRemoved, result.FilesFound, result.SuccessRate);
            
            if (result.TotalErrors > 0)
                msg += string.Format(Resources.Log_Ignored, result.TotalErrors, result.AccessDeniedErrors, result.FileInUseErrors);

            OnLogItem?.Invoke(new CleanupLogItem
            {
                Message = msg,
                Icon = "Checkmark24",
                StatusColor = "Green"
            });
        }
        else
        {
            string msg = string.Format(Resources.Log_Clean ?? "{0} : Clean", label, 0, 100);
            OnLogItem?.Invoke(new CleanupLogItem { Message = msg, Icon = "Info24", StatusColor = "Gray" });
        }
    }

    private CleanupResult CleanDirectory(string path, string? label, bool logOutput)
    {
        var result = new CleanupResult();
        var dirInfo = new DirectoryInfo(path);

        try {
            foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                try
                {
                    long size = file.Length;
                    result.RegisterFileFound(size);
                    file.Delete();
                    if (!file.Exists) result.RegisterFileRemoved(size);
                }
                catch (UnauthorizedAccessException)
                {
                    result.RegisterAccessDeniedError();
                    Logger.Log($"Falha por acesso negado ao remover arquivo '{file.FullName}'.", "WARNING");
                }
                catch (IOException ex)
                {
                    result.RegisterFileInUseError();
                    Logger.Log($"Falha por arquivo em uso ao remover '{file.FullName}': {ex.Message}", "WARNING");
                }
                catch (Exception ex)
                {
                    result.RegisterOtherError();
                    Logger.Log($"Falha ao remover arquivo '{file.FullName}': {ex.Message}", "WARNING");
                }
            }
        }
        catch (Exception ex)
        {
            result.RegisterOtherError();
            Logger.Log($"Erro ao enumerar arquivos em '{path}': {ex.Message}", "ERROR");
            OnLogItem?.Invoke(new CleanupLogItem { Message = $"{(label ?? path)}: erro ao enumerar arquivos ({ex.Message})", Icon = "Warning24", StatusColor = "Orange" });
        }

        try {
            foreach (var dir in dirInfo.EnumerateDirectories("*", SearchOption.AllDirectories))
            {
                try { dir.Delete(true); }
                catch (UnauthorizedAccessException)
                {
                    result.RegisterAccessDeniedError();
                    Logger.Log($"Falha por acesso negado ao remover diretório '{dir.FullName}'.", "WARNING");
                }
                catch (IOException ex)
                {
                    result.RegisterFileInUseError();
                    Logger.Log($"Falha por diretório em uso ao remover '{dir.FullName}': {ex.Message}", "WARNING");
                }
                catch (Exception ex)
                {
                    result.RegisterOtherError();
                    Logger.Log($"Falha ao remover diretório '{dir.FullName}': {ex.Message}", "WARNING");
                }
            }
        }
        catch (Exception ex)
        {
            result.RegisterOtherError();
            Logger.Log($"Erro ao enumerar diretórios em '{path}': {ex.Message}", "ERROR");
            OnLogItem?.Invoke(new CleanupLogItem { Message = $"{(label ?? path)}: erro ao enumerar diretórios ({ex.Message})", Icon = "Warning24", StatusColor = "Orange" });
        }

        if (logOutput && label != null)
        {
            LogAggregateResult(label, result);
        }

        return result;
    }

    private async Task<CleanupResult> CleanWindowsUpdateAsync()
    {
        var result = new CleanupResult();
        string wuPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download");
        if (!Directory.Exists(wuPath)) return result;

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
                    var res = CleanDirectory(wuPath, null, false);
                    result.Merge(res);
                    success = true;
                    // Log manual para WU
                    LogAggregateResult("Windows Update", res);
                }
                catch (Exception ex)
                {
                    Logger.Log($"Tentativa {attempts + 1} de limpeza do Windows Update falhou: {ex.Message}", "WARNING");
                    attempts++;
                    await Task.Delay(500); 
                }
            }

            if (!success)
            {
                result.RegisterOtherError();
                OnLogItem?.Invoke(new CleanupLogItem { Message = Resources.Log_WUError, Icon = "Warning24", StatusColor = "Orange" });
            }

        OnLogItem?.Invoke(new CleanupLogItem
        {
            result.RegisterOtherError();
            OnLogItem?.Invoke(new CleanupLogItem { Message = Resources.Log_WUFailStop, Icon = "Warning24", StatusColor = "Orange" });
        }

        return result;
    }

    private static void UpdateTotals(CleanupResult totals, CleanupResult current)
    {
        totals.BytesRemoved += current.BytesRemoved;
        totals.ItemsRemoved += current.ItemsRemoved;
        totals.ItemsIgnored += current.ItemsIgnored;
        totals.Failures += current.Failures;
        totals.Duration += current.Duration;
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
