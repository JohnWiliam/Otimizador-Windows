using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using SystemOptimizer.Models;
using SystemOptimizer.Properties;

namespace SystemOptimizer.Services;

public class CleanupService
{
    private readonly CleanupExecutionEngine _executionEngine = new();

    public event Action<CleanupLogItem>? OnLogItem;
    public event Action<CleanupProgressInfo>? OnProgress;

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

    public Task RunCleanupAsync() => RunCleanupAsync(CleanupOptions.CreateDefault(), CancellationToken.None);

    public Task RunCleanupAsync(CleanupOptions options) => RunCleanupAsync(options, CancellationToken.None);

    public async Task<IReadOnlyList<CleanupCategoryResult>> RunScanAsync(CleanupOptions options, CancellationToken cancellationToken = default)
    {
        var selectedCategories = BuildPlan(options);
        var results = new List<CleanupCategoryResult>();

        ReportProgress(0, "Iniciando análise", 0, selectedCategories.Count);

        for (var index = 0; index < selectedCategories.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var category = selectedCategories[index];

            ReportProgress(ToPercent(index, selectedCategories.Count), $"Analisando: {category.DisplayName}", 0, selectedCategories.Count);

            var scanResult = await category.ScanAction(cancellationToken);
            results.Add(new CleanupCategoryResult(category.Key, category.DisplayName, scanResult.Bytes, scanResult.Items, true));

            ReportProgress(ToPercent(index + 1, selectedCategories.Count), $"Análise concluída: {category.DisplayName}", scanResult.Items, selectedCategories.Count);
        }

        OnLogItem?.Invoke(new CleanupLogItem
        {
            Message = $"Análise concluída. Categorias avaliadas: {results.Count}.",
            Icon = "CheckmarkCircle24",
            StatusColor = "#0078D4",
            IsBold = true
        });

        return results;
    }

    public async Task RunCleanupAsync(CleanupOptions options, CancellationToken cancellationToken = default)
    {
        var selectedCategories = BuildPlan(options);

        ReportProgress(0, "Iniciando limpeza", 0, selectedCategories.Count);
        OnLogItem?.Invoke(new CleanupLogItem { Message = Resources.Log_Starting, Icon = "Play24", StatusColor = "#0078D4", IsBold = true });

        long totalBytes = 0;
        int totalIgnored = 0;
        int totalFailures = 0;
        int successItems = 0;

        for (var index = 0; index < selectedCategories.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var category = selectedCategories[index];

            ReportProgress(ToPercent(index, selectedCategories.Count), $"Limpando: {category.DisplayName}", 0, selectedCategories.Count);

            var result = await category.CleanAction(cancellationToken);
            totalBytes += result.Bytes;
            totalIgnored += result.Skipped;
            totalFailures += result.Failures;
            successItems++;

            if (category.LogSummary)
                LogAggregateResult(category.DisplayName, result.Bytes, result.Skipped);

            ReportProgress(ToPercent(index + 1, selectedCategories.Count), $"Concluído: {category.DisplayName}", result.Items, selectedCategories.Count);
        }

        double totalMb = Math.Round(totalBytes / 1024.0 / 1024.0, 2);
        OnLogItem?.Invoke(new CleanupLogItem
        {
            Message = string.Format(Resources.Log_Finished, totalMb),
            Icon = "CheckmarkCircle24",
            StatusColor = "#0078D4",
            IsBold = true
        });

        OnLogItem?.Invoke(new CleanupLogItem
        {
            Message = $"Resumo: sucesso={successItems}, ignorados={totalIgnored}, falhas={totalFailures}",
            Icon = "Info24",
            StatusColor = totalFailures > 0 ? "Orange" : "Green"
        });
    }

    private List<CleanupCategoryPlanItem> BuildPlan(CleanupOptions options)
    {
        var plan = new List<CleanupCategoryPlanItem>();

        if (options.CleanWindowsUpdate)
            plan.Add(new CleanupCategoryPlanItem("windows-update", "Windows Update", token => ScanWindowsUpdateAsync(token), token => CleanWindowsUpdateAsync(token), false));

        if (options.CleanUserTemp)
            plan.Add(new CleanupCategoryPlanItem("user-temp", Resources.Label_TempFiles ?? "User Temp", token => ScanUserTempAsync(token), token => CleanUserTempAsync(token), false));

        if (options.CleanSystemTemp)
            plan.Add(new CleanupCategoryPlanItem("system-temp", Resources.Label_SystemTemp ?? "System Temp", token => ScanSystemTempAsync(token), token => CleanSystemTempAsync(token), false));

        if (options.CleanPrefetch)
            plan.Add(new CleanupCategoryPlanItem("prefetch", "Prefetch", token => ScanDirectoryAsync(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch"), token), token => CleanDirectoryAsync(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch"), "Prefetch", token), true));

        if (options.CleanBrowserCache)
            plan.Add(new CleanupCategoryPlanItem("browser-cache", Resources.Label_BrowserCache ?? "Browser Cache", token => ScanBrowsersUnifiedAsync(token), token => CleanBrowsersUnifiedAsync(token), false));

        if (options.CleanDns)
            plan.Add(new CleanupCategoryPlanItem("dns", "DNS Cache", token => Task.FromResult(new DirectoryOperationResult(0, 1, 0, 0)), token => CleanDnsAsync(token), false));

        if (options.CleanRecycleBin)
            plan.Add(new CleanupCategoryPlanItem("recycle-bin", "Lixeira", token => Task.FromResult(new DirectoryOperationResult(0, 1, 0, 0)), token => CleanRecycleBinAsync(token), false));

        return plan;
    }

    private async Task<DirectoryOperationResult> CleanUserTempAsync(CancellationToken cancellationToken)
    {
        long totalBytes = 0;
        int totalSkipped = 0;
        int totalFailures = 0;
        int totalItems = 0;

        var userPaths = new Dictionary<string, string>
        {
            { Resources.Label_TempFiles ?? "User Temp", Path.GetTempPath() },
            { "WER Reports", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "WER") },
            { "CrashDumps", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps") }
        };

        foreach (var kvp in userPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(kvp.Value))
                continue;

            var res = await CleanDirectoryAsync(kvp.Value, kvp.Key, cancellationToken);
            totalBytes += res.Bytes;
            totalSkipped += res.Skipped;
            totalFailures += res.Failures;
            totalItems += res.Items;
            LogAggregateResult(kvp.Key, res.Bytes, res.Skipped);
        }

        var shaderPaths = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NVIDIA", "DXCache"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3DSCache"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AMD", "DxCache")
        };

        long shaderBytes = 0;
        int shaderSkipped = 0;
        int shaderItems = 0;

        foreach (var path in shaderPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(path))
                continue;

            var res = await CleanDirectoryAsync(path, null, cancellationToken);
            shaderBytes += res.Bytes;
            shaderSkipped += res.Skipped;
            shaderItems += res.Items;
            totalFailures += res.Failures;
        }

        LogAggregateResult(Resources.Label_ShaderCache ?? "Shader Cache", shaderBytes, shaderSkipped);
        totalBytes += shaderBytes;
        totalSkipped += shaderSkipped;
        totalItems += shaderItems;

        return new DirectoryOperationResult(totalBytes, totalItems, totalSkipped, totalFailures);
    }

    private async Task<DirectoryOperationResult> ScanUserTempAsync(CancellationToken cancellationToken)
    {
        long totalBytes = 0;
        int totalItems = 0;

        var userPaths = new[]
        {
            Path.GetTempPath(),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "WER"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NVIDIA", "DXCache"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3DSCache"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AMD", "DxCache")
        };

        foreach (var path in userPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var res = await ScanDirectoryAsync(path, cancellationToken);
            totalBytes += res.Bytes;
            totalItems += res.Items;
        }

        return new DirectoryOperationResult(totalBytes, totalItems, 0, 0);
    }

    private async Task<DirectoryOperationResult> CleanSystemTempAsync(CancellationToken cancellationToken)
    {
        long totalBytes = 0;
        int totalItems = 0;
        int totalSkipped = 0;
        int totalFailures = 0;

        var sysPaths = new Dictionary<string, string>
        {
            { Resources.Label_SystemTemp ?? "System Temp", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp") },
            { "Windows Logs", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Logs") }
        };

        foreach (var kvp in sysPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(kvp.Value))
                continue;

            var res = await CleanDirectoryAsync(kvp.Value, kvp.Key, cancellationToken);
            totalBytes += res.Bytes;
            totalItems += res.Items;
            totalSkipped += res.Skipped;
            totalFailures += res.Failures;
            LogAggregateResult(kvp.Key, res.Bytes, res.Skipped);
        }

        return new DirectoryOperationResult(totalBytes, totalItems, totalSkipped, totalFailures);
    }

    private async Task<DirectoryOperationResult> ScanSystemTempAsync(CancellationToken cancellationToken)
    {
        var temp = await ScanDirectoryAsync(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"), cancellationToken);
        var logs = await ScanDirectoryAsync(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Logs"), cancellationToken);
        return temp.Combine(logs);
    }

    private async Task<DirectoryOperationResult> CleanBrowsersUnifiedAsync(CancellationToken cancellationToken)
    {
        long totalBytes = 0;
        int totalItems = 0;
        int totalSkipped = 0;
        int totalFailures = 0;

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        string chromePath = Path.Combine(localAppData, "Google", "Chrome", "User Data");
        var chromeRes = await CleanChromiumBrowserAsync(chromePath, cancellationToken);
        totalBytes += chromeRes.Bytes;
        totalItems += chromeRes.Items;
        totalSkipped += chromeRes.Skipped;
        totalFailures += chromeRes.Failures;

        string edgePath = Path.Combine(localAppData, "Microsoft", "Edge", "User Data");
        var edgeRes = await CleanChromiumBrowserAsync(edgePath, cancellationToken);
        totalBytes += edgeRes.Bytes;
        totalItems += edgeRes.Items;
        totalSkipped += edgeRes.Skipped;
        totalFailures += edgeRes.Failures;

        string firefoxPath = Path.Combine(localAppData, "Mozilla", "Firefox", "Profiles");
        if (Directory.Exists(firefoxPath))
        {
            foreach (var dir in Directory.GetDirectories(firefoxPath))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string cachePath = Path.Combine(dir, "cache2", "entries");
                if (!Directory.Exists(cachePath))
                    continue;

                var res = await CleanDirectoryAsync(cachePath, null, cancellationToken);
                totalBytes += res.Bytes;
                totalItems += res.Items;
                totalSkipped += res.Skipped;
                totalFailures += res.Failures;
            }
        }

        LogAggregateResult(Resources.Label_BrowserCache ?? "Browser Cache", totalBytes, totalSkipped);
        return new DirectoryOperationResult(totalBytes, totalItems, totalSkipped, totalFailures);
    }

    private async Task<DirectoryOperationResult> ScanBrowsersUnifiedAsync(CancellationToken cancellationToken)
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var total = new DirectoryOperationResult(0, 0, 0, 0);

        total = total.Combine(await ScanChromiumBrowserAsync(Path.Combine(localAppData, "Google", "Chrome", "User Data"), cancellationToken));
        total = total.Combine(await ScanChromiumBrowserAsync(Path.Combine(localAppData, "Microsoft", "Edge", "User Data"), cancellationToken));

        string firefoxPath = Path.Combine(localAppData, "Mozilla", "Firefox", "Profiles");
        if (Directory.Exists(firefoxPath))
        {
            foreach (var dir in Directory.GetDirectories(firefoxPath))
            {
                cancellationToken.ThrowIfCancellationRequested();
                total = total.Combine(await ScanDirectoryAsync(Path.Combine(dir, "cache2", "entries"), cancellationToken));
            }
        }

        return total;
    }

    private async Task<DirectoryOperationResult> ScanChromiumBrowserAsync(string userDataPath, CancellationToken cancellationToken)
    {
        var total = new DirectoryOperationResult(0, 0, 0, 0);
        if (!Directory.Exists(userDataPath))
            return total;

        foreach (var dir in Directory.GetDirectories(userDataPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(Path.Combine(dir, "Preferences")) && !dir.EndsWith("Default", StringComparison.OrdinalIgnoreCase) && !dir.Contains("Profile", StringComparison.OrdinalIgnoreCase))
                continue;

            total = total.Combine(await ScanDirectoryAsync(Path.Combine(dir, "Cache", "Cache_Data"), cancellationToken));
        }

        return total;
    }

    private async Task<DirectoryOperationResult> CleanChromiumBrowserAsync(string userDataPath, CancellationToken cancellationToken)
    {
        var total = new DirectoryOperationResult(0, 0, 0, 0);
        if (!Directory.Exists(userDataPath))
            return total;

        foreach (var dir in Directory.GetDirectories(userDataPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(Path.Combine(dir, "Preferences")) && !dir.EndsWith("Default", StringComparison.OrdinalIgnoreCase) && !dir.Contains("Profile", StringComparison.OrdinalIgnoreCase))
                continue;

            total = total.Combine(await CleanDirectoryAsync(Path.Combine(dir, "Cache", "Cache_Data"), null, cancellationToken));
        }

        return total;
    }

    private Task<DirectoryOperationResult> ScanWindowsUpdateAsync(CancellationToken cancellationToken)
    {
        string wuPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download");
        return ScanDirectoryAsync(wuPath, cancellationToken);
    }

    private async Task<DirectoryOperationResult> CleanWindowsUpdateAsync(CancellationToken cancellationToken)
    {
        string wuPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download");
        if (!Directory.Exists(wuPath))
            return new DirectoryOperationResult(0, 0, 0, 0);

        string[] services = ["wuauserv", "bits", "cryptsvc"];
        bool stopped = await ToggleServicesAsync(services, false, cancellationToken);

        if (!stopped)
        {
            OnLogItem?.Invoke(new CleanupLogItem { Message = Resources.Log_WUFailStop, Icon = "Warning24", StatusColor = "Orange" });
            return new DirectoryOperationResult(0, 0, 0, 1);
        }

        OnLogItem?.Invoke(new CleanupLogItem { Message = Resources.Log_WUServicesStopped, Icon = "Pause24", StatusColor = "#CA5010" });

        var res = await CleanDirectoryAsync(wuPath, null, cancellationToken);

        await ToggleServicesAsync(services, true, cancellationToken);
        OnLogItem?.Invoke(new CleanupLogItem { Message = Resources.Log_WUServicesRestarted, Icon = "Play24", StatusColor = "Green" });

        return res;
    }

    private Task<DirectoryOperationResult> CleanDnsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            CommandHelper.RunCommand("powershell", "Clear-DnsClientCache");
            OnLogItem?.Invoke(new CleanupLogItem { Message = Resources.Log_DNSCleared, Icon = "Globe24", StatusColor = "Green" });
            return Task.FromResult(new DirectoryOperationResult(0, 1, 0, 0));
        }

        foreach (var dir in directoriesToDelete.OrderByDescending(GetDirectoryDepth))
        {
            Logger.Log($"Falha ao limpar cache DNS: {ex.Message}", "ERROR");
            OnLogItem?.Invoke(new CleanupLogItem { Message = $"DNS: falha ao limpar cache ({ex.Message})", Icon = "Warning24", StatusColor = "Orange" });
            return Task.FromResult(new DirectoryOperationResult(0, 1, 0, 1));
        }
    }

    private Task<DirectoryOperationResult> CleanRecycleBinAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
            OnLogItem?.Invoke(new CleanupLogItem { Message = Resources.Log_RecycleBinEmptied, Icon = "Delete24", StatusColor = "Green" });
            return Task.FromResult(new DirectoryOperationResult(0, 1, 0, 0));
        }
        catch (Exception ex)
        {
            Logger.Log($"Falha ao esvaziar lixeira: {ex.Message}", "ERROR");
            OnLogItem?.Invoke(new CleanupLogItem { Message = $"Lixeira: falha ao esvaziar ({ex.Message})", Icon = "Warning24", StatusColor = "Orange" });
            return Task.FromResult(new DirectoryOperationResult(0, 1, 0, 1));
        }
    }

    private Task<DirectoryOperationResult> ScanDirectoryAsync(string path, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            long bytes = 0;
            int items = 0;

            if (!Directory.Exists(path))
                return new DirectoryOperationResult(0, 0, 0, 0);

            try
            {
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var info = new FileInfo(file);
                        bytes += info.Length;
                        items++;
                    }
                    catch
                    {
                        // Ignora arquivos inacessíveis na análise.
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Erro ao analisar diretório '{path}': {ex.Message}", "WARNING");
            }

            return new DirectoryOperationResult(bytes, items, 0, 0);
        }, cancellationToken);
    }

    private Task<DirectoryOperationResult> CleanDirectoryAsync(string path, string? label, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            long categoryBytes = 0;
            int processedItems = 0;
            int skippedCount = 0;
            int failures = 0;

            if (!Directory.Exists(path))
                return new DirectoryOperationResult(0, 0, 0, 0);

            try
            {
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var info = new FileInfo(file);
                        long size = info.Length;
                        info.Delete();
                        if (!info.Exists)
                            categoryBytes += size;

                        processedItems++;
                    }
                    catch (Exception ex)
                    {
                        skippedCount++;
                        failures++;
                        Logger.Log($"Falha ao remover arquivo '{file}': {ex.Message}", "WARNING");
                    }
                }
            }
            catch (Exception ex)
            {
                failures++;
                Logger.Log($"Erro ao enumerar arquivos em '{path}': {ex.Message}", "ERROR");
                OnLogItem?.Invoke(new CleanupLogItem { Message = $"{(label ?? path)}: erro ao enumerar arquivos ({ex.Message})", Icon = "Warning24", StatusColor = "Orange" });
            }

            try
            {
                foreach (var dir in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories).OrderByDescending(x => x.Length))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try { Directory.Delete(dir, true); }
                    catch (Exception ex)
                    {
                        skippedCount++;
                        failures++;
                        Logger.Log($"Falha ao remover diretório '{dir}': {ex.Message}", "WARNING");
                    }
                }
            }
            catch (Exception ex)
            {
                failures++;
                Logger.Log($"Erro ao enumerar diretórios em '{path}': {ex.Message}", "ERROR");
                OnLogItem?.Invoke(new CleanupLogItem { Message = $"{(label ?? path)}: erro ao enumerar diretórios ({ex.Message})", Icon = "Warning24", StatusColor = "Orange" });
            }
        }
        finally
        {
            restoreResults = await ToggleServicesAsync(services, true);
            bool allRestored = restoreResults.TrueForAll(r => r.IsSuccess);

            return new DirectoryOperationResult(categoryBytes, processedItems, skippedCount, failures);
        }, cancellationToken);
    }

    private static async Task<bool> ToggleServicesAsync(string[] services, bool start, CancellationToken cancellationToken)
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
                    cancellationToken.ThrowIfCancellationRequested();
                    using var sc = new ServiceController(s);
                    if (start)
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

                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Falha ao {(start ? "iniciar" : "parar")} serviços do Windows Update: {ex.Message}", "ERROR");
                return false;
            }
        }, cancellationToken);
    }

    private void ReportProgress(int percentage, string currentCategory, int processedItems, int totalSteps)
    {
        OnProgress?.Invoke(new CleanupProgressInfo(percentage, currentCategory, processedItems, totalSteps));
    }

    private static int ToPercent(int currentStep, int totalSteps)
    {
        if (totalSteps <= 0)
            return 100;

        return (int)Math.Round((double)currentStep / totalSteps * 100, MidpointRounding.AwayFromZero);
    }

    private void LogAggregateResult(string label, long bytes, int skipped)
    {
        if (bytes > 0)
        {
            double mb = Math.Round(bytes / 1024.0 / 1024.0, 2);
            string msg = string.Format(Resources.Log_Removed, label, mb);
            if (skipped > 0)
                msg += string.Format(Resources.Log_Ignored, skipped);

            OnLogItem?.Invoke(new CleanupLogItem { Message = msg, Icon = "Checkmark24", StatusColor = "Green" });
        }
        else
        {
            string msg = string.Format(Resources.Log_Clean ?? "{0} : Clean", label);
            OnLogItem?.Invoke(new CleanupLogItem { Message = msg, Icon = "Info24", StatusColor = "Gray" });
        }
    }

    private sealed record CleanupCategoryPlanItem(
        string Key,
        string DisplayName,
        Func<CancellationToken, Task<DirectoryOperationResult>> ScanAction,
        Func<CancellationToken, Task<DirectoryOperationResult>> CleanAction,
        bool LogSummary);

    private sealed record DirectoryOperationResult(long Bytes, int Items, int Skipped, int Failures)
    {
        public DirectoryOperationResult Combine(DirectoryOperationResult other) =>
            new(Bytes + other.Bytes, Items + other.Items, Skipped + other.Skipped, Failures + other.Failures);
    }

}

public sealed record CleanupCategoryResult(string Key, string DisplayName, long Bytes, int Items, bool IsSelected);

public sealed record CleanupProgressInfo(int Percentage, string CurrentCategory, int ProcessedItems, int TotalSteps);

public class CleanupOptions
{
    public bool CleanUserTemp { get; set; } = true;
    public bool CleanSystemTemp { get; set; } = true;
    public bool CleanPrefetch { get; set; } = true;
    public bool CleanBrowserCache { get; set; } = true;
    public bool CleanDns { get; set; } = true;
    public bool CleanWindowsUpdate { get; set; } = true;
    public bool CleanRecycleBin { get; set; } = false;

    public static CleanupOptions CreateDefault() => new();
}
