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

    private sealed record ServiceToggleResult(string ServiceName, ServiceControllerStatus? InitialStatus, string Operation, ServiceControllerStatus? FinalStatus, string? Error)
    {
        public bool IsSuccess => string.IsNullOrEmpty(Error);
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
                var dnsResult = CommandHelper.RunCommandDetailed("powershell", "Clear-DnsClientCache");
                if (dnsResult.IsSuccess)
                {
                    OnLogItem?.Invoke(new CleanupLogItem { Message = Resources.Log_DNSCleared, Icon = "Globe24", StatusColor = "Green" });
                    MergeTargetResult(targetResults, "DNS", CleanupResult.SuccessfulOperation());
                }
                else
                {
                    totalFailures++;
                    string dnsError = dnsResult.TimedOut
                        ? "timeout na execução"
                        : (!string.IsNullOrWhiteSpace(dnsResult.StdErr) ? dnsResult.StdErr.Trim() : $"ExitCode={dnsResult.ExitCode?.ToString() ?? "N/A"}");

                    Logger.Log($"Falha ao limpar cache DNS: {dnsError}", "ERROR");
                    OnLogItem?.Invoke(new CleanupLogItem { Message = $"DNS: falha ao limpar cache ({dnsError})", Icon = "Warning24", StatusColor = "Orange" });
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
        long categoryBytes = 0;
        int skippedCount = 0;
        int failures = 0;
        const bool bestEffortByCategory = true;
        var rootDirectory = new DirectoryInfo(path);
        var visitedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var directoriesToDelete = new List<DirectoryInfo>();
        var directoriesToTraverse = new Stack<DirectoryInfo>();
        var categoryErrorCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        directoriesToTraverse.Push(rootDirectory);

        while (directoriesToTraverse.Count > 0)
        {
            var currentDirectory = directoriesToTraverse.Pop();

            if (!visitedDirectories.Add(currentDirectory.FullName))
            {
                continue;
            }

            directoriesToDelete.Add(currentDirectory);

            IEnumerable<FileInfo> files;
            try
            {
                files = currentDirectory.EnumerateFiles();
            }
            catch (Exception ex)
            {
                RegisterException(
                    categoryErrorCounts,
                    bestEffortByCategory,
                    operation: "EnumerateFiles",
                    targetPath: currentDirectory.FullName,
                    ex,
                    ref skippedCount,
                    ref failures);
                continue;
            }

            foreach (var file in files)
            {
                try
                {
                    long size = file.Length;
                    result.RegisterFileFound(size);
                    file.Delete();
                    if (!file.Exists)
                    {
                        categoryBytes += size;
                    }
                }
                catch (Exception ex)
                {
                    RegisterException(
                        categoryErrorCounts,
                        bestEffortByCategory,
                        operation: "DeleteFile",
                        targetPath: file.FullName,
                        ex,
                        ref skippedCount,
                        ref failures);
                }
            }

            IEnumerable<DirectoryInfo> childDirectories;
            try
            {
                childDirectories = currentDirectory.EnumerateDirectories();
            }
            catch (Exception ex)
            {
                RegisterException(
                    categoryErrorCounts,
                    bestEffortByCategory,
                    operation: "EnumerateDirectories",
                    targetPath: currentDirectory.FullName,
                    ex,
                    ref skippedCount,
                    ref failures);
                continue;
            }

            foreach (var childDirectory in childDirectories)
            {
                if ((childDirectory.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                {
                    skippedCount++;
                    Logger.Log($"Ignorando reparse point '{childDirectory.FullName}' durante limpeza.", "INFO");
                    continue;
                }

                directoriesToTraverse.Push(childDirectory);
            }
        }

        foreach (var dir in directoriesToDelete.OrderByDescending(GetDirectoryDepth))
        {
            if (string.Equals(dir.FullName, rootDirectory.FullName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                dir.Delete(false);
            }
            catch (Exception ex)
            {
                RegisterException(
                    categoryErrorCounts,
                    bestEffortByCategory,
                    operation: "DeleteDirectory",
                    targetPath: dir.FullName,
                    ex,
                    ref skippedCount,
                    ref failures);
            }
        }

        foreach (var kvp in categoryErrorCounts)
        {
            if (kvp.Value <= 1)
            {
                continue;
            }

            Logger.Log($"Categoria '{kvp.Key}' teve {kvp.Value} ocorrência(s); logs adicionais foram suprimidos por best effort.", "INFO");
        }

        if (logOutput && label != null)
        {
            LogAggregateResult(label, result);
        }

        return result;
    }

    private static int GetDirectoryDepth(DirectoryInfo directory)
    {
        int depth = 0;
        var current = directory.Parent;
        while (current != null)
        {
            depth++;
            current = current.Parent;
        }

        return depth;
    }

    private static void RegisterException(
        Dictionary<string, int> categoryErrorCounts,
        bool bestEffortByCategory,
        string operation,
        string targetPath,
        Exception ex,
        ref int skippedCount,
        ref int failures)
    {
        skippedCount++;
        failures++;

        string exceptionKind = ClassifyException(ex);
        string category = $"{operation}:{exceptionKind}";

        categoryErrorCounts.TryGetValue(category, out int currentCount);
        currentCount++;
        categoryErrorCounts[category] = currentCount;

        if (bestEffortByCategory && currentCount > 1)
        {
            return;
        }

        Logger.Log($"[{category}] Falha em '{targetPath}': {ex.Message}", "WARNING");
    }

    private static string ClassifyException(Exception ex)
    {
        return ex switch
        {
            UnauthorizedAccessException => nameof(UnauthorizedAccessException),
            IOException => nameof(IOException),
            PathTooLongException => nameof(PathTooLongException),
            DirectoryNotFoundException => nameof(DirectoryNotFoundException),
            FileNotFoundException => nameof(FileNotFoundException),
            _ => ex.GetType().Name
        };
    }

    private async Task CleanWindowsUpdateAsync()
    {
        var result = new CleanupResult();
        string wuPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download");
        if (!Directory.Exists(wuPath)) return result;

        string[] services = ["wuauserv", "bits", "cryptsvc"];
        List<ServiceToggleResult>? stopResults = null;
        List<ServiceToggleResult>? restoreResults = null;

        try
        {
            stopResults = await ToggleServicesAsync(services, false);
            bool allStopped = stopResults.TrueForAll(r => r.IsSuccess);

            if (!allStopped)
            {
                OnLogItem?.Invoke(new CleanupLogItem { Message = Resources.Log_WUFailStop, Icon = "Warning24", StatusColor = "Orange" });
                return;
            }

            OnLogItem?.Invoke(new CleanupLogItem { Message = Resources.Log_WUServicesStopped, Icon = "Pause24", StatusColor = "#CA5010" });

            bool success = false;
            int attempts = 0;
            while (!success && attempts < 5)
            {
                attempts++;
                var res = CleanDirectory(wuPath, null, false);
                if (res.Failures == 0)
                {
                    success = true;
                    if (res.Bytes > 0)
                    {
                        LogAggregateResult("Windows Update", res.Bytes, res.Skipped);
                    }
                    else
                    {
                        OnLogItem?.Invoke(new CleanupLogItem { Message = string.Format(Resources.Log_Clean, "Windows Update"), Icon = "Checkmark24", StatusColor = "Green" });
                    }
                }
                else
                {
                    Logger.Log($"Tentativa {attempts} de limpeza do Windows Update teve {res.Failures} falha(s).", "WARNING");
                    if (attempts < 5)
                    {
                        await Task.Delay(500);
                    }
                }
            }

            if (!success)
            {
                result.RegisterOtherError();
                OnLogItem?.Invoke(new CleanupLogItem { Message = Resources.Log_WUError, Icon = "Warning24", StatusColor = "Orange" });
            }
        }
        finally
        {
            restoreResults = await ToggleServicesAsync(services, true);
            bool allRestored = restoreResults.TrueForAll(r => r.IsSuccess);

            if (allRestored)
            {
                OnLogItem?.Invoke(new CleanupLogItem { Message = Resources.Log_WUServicesRestarted, Icon = "Play24", StatusColor = "Green" });
            }
            else
            {
                OnLogItem?.Invoke(new CleanupLogItem { Message = "Windows Update: falha ao restaurar um ou mais serviços.", Icon = "Warning24", StatusColor = "Orange" });
            }

            LogServiceStateAudit(stopResults, restoreResults);
        }
    }

    private void LogServiceStateAudit(List<ServiceToggleResult>? stopResults, List<ServiceToggleResult>? restoreResults)
    {
        OnLogItem?.Invoke(new CleanupLogItem
        {
            Message = "Estado de serviços:",
            Icon = "Info24",
            StatusColor = "Gray",
            IsBold = true
        });

        void Emit(string etapa, List<ServiceToggleResult>? results)
        {
            if (results == null || results.Count == 0)
            {
                OnLogItem?.Invoke(new CleanupLogItem { Message = $" - {etapa}: sem dados.", Icon = "Info24", StatusColor = "Gray" });
                return;
            }

            foreach (var result in results)
            {
                string detail = $" - [{etapa}] {result.ServiceName}: inicial={result.InitialStatus?.ToString() ?? "N/A"}, operacao={result.Operation}, final={result.FinalStatus?.ToString() ?? "N/A"}";
                if (!string.IsNullOrWhiteSpace(result.Error))
                {
                    detail += $", erro={result.Error}";
                }

                OnLogItem?.Invoke(new CleanupLogItem
                {
                    Message = detail,
                    Icon = result.IsSuccess ? "Checkmark24" : "Warning24",
                    StatusColor = result.IsSuccess ? "Green" : "Orange"
                });
            }
        }

        Emit("parada", stopResults);
        Emit("restauracao", restoreResults);
    }

    private static async Task<List<ServiceToggleResult>> ToggleServicesAsync(string[] services, bool start)
    {
        return await Task.Run(() =>
        {
            var results = new List<ServiceToggleResult>();
            string operation = start ? "start" : "stop";
            ServiceControllerStatus targetStatus = start ? ServiceControllerStatus.Running : ServiceControllerStatus.Stopped;

            foreach (var serviceName in services)
            {
                ServiceControllerStatus? initialStatus = null;
                ServiceControllerStatus? finalStatus = null;
                string? error = null;

                try
                {
                    using var sc = new ServiceController(serviceName);
                    initialStatus = sc.Status;

                    if (sc.Status != targetStatus)
                    {
                        if (start)
                        {
                            sc.Start();
                        }
                        else
                        {
                            sc.Stop();
                        }

                        sc.WaitForStatus(targetStatus, TimeSpan.FromSeconds(10));
                        sc.Refresh();
                    }

                    finalStatus = sc.Status;
                    if (finalStatus != targetStatus)
                    {
                        error = $"estado final inesperado: {finalStatus}";
                    }
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }

                if (!string.IsNullOrWhiteSpace(error))
                {
                    Logger.Log($"Falha ao {operation} serviço '{serviceName}': {error}", "ERROR");
                }

                results.Add(new ServiceToggleResult(serviceName, initialStatus, operation, finalStatus, error));
            }

            return results;
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
