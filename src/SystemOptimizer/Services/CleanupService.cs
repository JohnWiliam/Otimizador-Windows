using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SystemOptimizer.Helpers;
using SystemOptimizer.Models;
using SystemOptimizer.Properties;

namespace SystemOptimizer.Services;

public class CleanupService
{
    private const string UserTempKey = "user-temp";
    private const string SystemTempKey = "system-temp";
    private const string PrefetchKey = "prefetch";
    private const string BrowserCacheKey = "browser-cache";
    private const string DnsKey = "dns";
    private const string WindowsUpdateKey = "windows-update";
    private const string RecycleBinKey = "recycle-bin";

    private readonly CleanupExecutionEngine _executionEngine;
    private readonly Dictionary<string, CleanupCategoryDefinition> _categoryMap;

    public event Action<CleanupLogItem>? OnLogItem;
    public event Action<CleanupProgressInfo>? OnProgress;

    public CleanupService(CleanupExecutionEngine executionEngine, IEnumerable<ICleanupTargetProvider> targetProviders)
    {
        _executionEngine = executionEngine;

        _categoryMap = new(StringComparer.OrdinalIgnoreCase)
        {
            [UserTempKey] = new(UserTempKey, Resources.Label_TempFiles ?? "User Temp", targetProviders.OfType<UserTempCleanupTargetProvider>().SelectMany(p => p.GetTargets()).ToList()),
            [SystemTempKey] = new(SystemTempKey, Resources.Label_SystemTemp ?? "System Temp", targetProviders.OfType<SystemTempCleanupTargetProvider>().SelectMany(p => p.GetTargets()).ToList()),
            [PrefetchKey] = new(PrefetchKey, "Prefetch", targetProviders.OfType<PrefetchCleanupTargetProvider>().SelectMany(p => p.GetTargets()).ToList()),
            [BrowserCacheKey] = new(BrowserCacheKey, Resources.Label_BrowserCache ?? "Browser Cache", targetProviders.OfType<BrowserCacheCleanupTargetProvider>().SelectMany(p => p.GetTargets()).ToList()),
            [DnsKey] = new(DnsKey, "DNS Cache", targetProviders.OfType<DnsCleanupTargetProvider>().SelectMany(p => p.GetTargets()).ToList()),
            [WindowsUpdateKey] = new(WindowsUpdateKey, "Windows Update", targetProviders.OfType<WindowsUpdateCleanupTargetProvider>().SelectMany(p => p.GetTargets()).ToList()),
            [RecycleBinKey] = new(RecycleBinKey, "Lixeira", targetProviders.OfType<RecycleBinCleanupTargetProvider>().SelectMany(p => p.GetTargets()).ToList())
        };
    }

    public Task RunCleanupAsync() => RunCleanupAsync(CleanupOptions.CreateDefault(), CancellationToken.None);

    public Task RunCleanupAsync(CleanupOptions options) => RunCleanupAsync(options, CancellationToken.None);

    public async Task<IReadOnlyList<CleanupCategoryResult>> RunScanAsync(CleanupOptions options, CancellationToken cancellationToken = default)
    {
        var plan = BuildPlan(options);
        var results = new List<CleanupCategoryResult>(plan.Count);
        int totalItems = 0;
        long totalBytes = 0;

        ReportProgress(0, "Iniciando análise", 0, plan.Count);

        for (var index = 0; index < plan.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var category = plan[index];
            ReportProgress(ToPercent(index, plan.Count), $"Analisando: {category.DisplayName}", 0, plan.Count);

            long bytes = 0;
            int items = 0;
            foreach (var target in category.Targets)
            {
                var scan = await ScanTargetAsync(target, cancellationToken);
                bytes += scan.Bytes;
                items += scan.Items;
            }

            totalBytes += bytes;
            totalItems += items;

            results.Add(new CleanupCategoryResult(category.Key, category.DisplayName, bytes, items, true));
            ReportProgress(ToPercent(index + 1, plan.Count), $"Análise concluída: {category.DisplayName}", items, plan.Count);
        }

        double totalMb = Math.Round(totalBytes / 1024d / 1024d, 2);

        OnLogItem?.Invoke(new CleanupLogItem
        {
            Message = $"Análise concluída. Categorias avaliadas: {results.Count}. Potencial: {totalMb} MB em {totalItems} item(ns).",
            Icon = "CheckmarkCircle24",
            StatusColor = "#0078D4",
            IsBold = true
        });

        return results;
    }

    public async Task RunCleanupAsync(CleanupOptions options, CancellationToken cancellationToken = default)
    {
        var plan = BuildPlan(options);
        ReportProgress(0, "Iniciando limpeza", 0, plan.Count);
        OnLogItem?.Invoke(new CleanupLogItem { Message = Resources.Log_Starting, Icon = "Play24", StatusColor = "#0078D4", IsBold = true });

        long totalBytes = 0;
        int totalItemsRemoved = 0;
        int totalIgnored = 0;
        int totalFailures = 0;

        for (var index = 0; index < plan.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var category = plan[index];
            ReportProgress(ToPercent(index, plan.Count), $"Limpando: {category.DisplayName}", 0, plan.Count);

            var aggregate = new CleanupResult { CategoryName = category.DisplayName };
            foreach (var target in category.Targets)
            {
                var targetResult = await _executionEngine.ExecuteAsync(target);
                aggregate.BytesRemoved += targetResult.BytesRemoved;
                aggregate.ItemsRemoved += targetResult.ItemsRemoved;
                aggregate.ItemsIgnored += targetResult.ItemsIgnored;
                aggregate.Failures += targetResult.Failures;
            }

            totalBytes += aggregate.BytesRemoved;
            totalItemsRemoved += aggregate.ItemsRemoved;
            totalIgnored += aggregate.ItemsIgnored;
            totalFailures += aggregate.Failures;

            LogAggregateResult(category.DisplayName, aggregate.BytesRemoved, aggregate.ItemsRemoved, aggregate.ItemsIgnored, aggregate.Failures);
            ReportProgress(ToPercent(index + 1, plan.Count), $"Concluído: {category.DisplayName}", aggregate.ItemsRemoved, plan.Count);
        }

        double totalMb = Math.Round(totalBytes / 1024d / 1024d, 2);
        OnLogItem?.Invoke(new CleanupLogItem
        {
            Message = FormatSafe(Resources.Log_Finished, totalMb, totalItemsRemoved, totalItemsRemoved + totalIgnored + totalFailures, CalculateSuccessPercent(totalItemsRemoved, totalIgnored, totalFailures)),
            Icon = "CheckmarkCircle24",
            StatusColor = "#0078D4",
            IsBold = true
        });

        OnLogItem?.Invoke(new CleanupLogItem
        {
            Message = $"Resumo: ignorados={totalIgnored}, falhas={totalFailures}",
            Icon = "Info24",
            StatusColor = totalFailures > 0 ? "Orange" : "Green"
        });
    }

    private List<CleanupCategoryDefinition> BuildPlan(CleanupOptions options)
    {
        var selectedKeys = new List<string>();

        if (options.CleanWindowsUpdate) selectedKeys.Add(WindowsUpdateKey);
        if (options.CleanUserTemp) selectedKeys.Add(UserTempKey);
        if (options.CleanSystemTemp) selectedKeys.Add(SystemTempKey);
        if (options.CleanPrefetch) selectedKeys.Add(PrefetchKey);
        if (options.CleanBrowserCache) selectedKeys.Add(BrowserCacheKey);
        if (options.CleanDns) selectedKeys.Add(DnsKey);
        if (options.CleanRecycleBin) selectedKeys.Add(RecycleBinKey);

        return selectedKeys
            .Where(key => _categoryMap.ContainsKey(key))
            .Select(key => _categoryMap[key])
            .Where(category => category.Targets.Count > 0)
            .ToList();
    }

    private async Task<ScanResult> ScanTargetAsync(CleanupTarget target, CancellationToken cancellationToken)
    {
        return target.Strategy switch
        {
            CleanupExecutionStrategy.DeleteDirectoryContents => await ScanDirectoryAsync(target.Path, cancellationToken),
            CleanupExecutionStrategy.CleanupWindowsUpdate => await ScanDirectoryAsync(target.Path, cancellationToken),
            CleanupExecutionStrategy.CleanupBrowserCache => await ScanBrowserCacheAsync(cancellationToken),
            CleanupExecutionStrategy.ExecuteCommand => new ScanResult(0, 1),
            CleanupExecutionStrategy.EmptyRecycleBin => new ScanResult(0, 1),
            _ => new ScanResult(0, 0)
        };
    }

    private static async Task<ScanResult> ScanBrowserCacheAsync(CancellationToken cancellationToken)
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        long bytes = 0;
        int items = 0;

        var chromiumRoots = new[]
        {
            Path.Combine(localAppData, "Google", "Chrome", "User Data"),
            Path.Combine(localAppData, "Microsoft", "Edge", "User Data")
        };

        foreach (var root in chromiumRoots)
        {
            if (!Directory.Exists(root))
                continue;

            foreach (var profileDir in Directory.GetDirectories(root))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsChromiumProfileDirectory(profileDir))
                    continue;

                var cacheScan = await ScanDirectoryAsync(Path.Combine(profileDir, "Cache", "Cache_Data"), cancellationToken);
                bytes += cacheScan.Bytes;
                items += cacheScan.Items;
            }
        }

        var firefoxProfiles = Path.Combine(localAppData, "Mozilla", "Firefox", "Profiles");
        if (Directory.Exists(firefoxProfiles))
        {
            foreach (var profile in Directory.GetDirectories(firefoxProfiles))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var scan = await ScanDirectoryAsync(Path.Combine(profile, "cache2", "entries"), cancellationToken);
                bytes += scan.Bytes;
                items += scan.Items;
            }
        }

        return new ScanResult(bytes, items);
    }

    private static bool IsChromiumProfileDirectory(string profileDir)
    {
        return File.Exists(Path.Combine(profileDir, "Preferences"))
            || profileDir.EndsWith("Default", StringComparison.OrdinalIgnoreCase)
            || profileDir.Contains("Profile", StringComparison.OrdinalIgnoreCase);
    }

    private static Task<ScanResult> ScanDirectoryAsync(string path, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            if (!Directory.Exists(path))
                return new ScanResult(0, 0);

            long bytes = 0;
            int items = 0;
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
                        // ignora arquivo inacessível durante análise
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Erro ao analisar diretório '{path}': {ex.Message}", "WARNING");
            }

            return new ScanResult(bytes, items);
        }, cancellationToken);
    }

    private void ReportProgress(int percentage, string currentCategory, int processedItems, int totalSteps)
        => OnProgress?.Invoke(new CleanupProgressInfo(percentage, currentCategory, processedItems, totalSteps));

    private static int ToPercent(int currentStep, int totalSteps)
    {
        if (totalSteps <= 0)
            return 100;

        return (int)Math.Round((double)currentStep / totalSteps * 100, MidpointRounding.AwayFromZero);
    }


    private static string FormatSafe(string? template, params object[] args)
    {
        if (string.IsNullOrWhiteSpace(template))
            return string.Empty;

        try
        {
            return string.Format(template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }
    private void LogAggregateResult(string label, long bytes, int itemsRemoved, int skipped, int failures)
    {
        if (bytes > 0)
        {
            double mb = Math.Round(bytes / 1024d / 1024d, 2);
            int totalProcessed = Math.Max(0, itemsRemoved + skipped + failures);
            int successPercent = CalculateSuccessPercent(itemsRemoved, skipped, failures);

            var message = FormatSafe(Resources.Log_Removed, label, mb, itemsRemoved, totalProcessed, successPercent);
            if (skipped > 0 || failures > 0)
                message += FormatSafe(Resources.Log_Ignored, skipped + failures, skipped, failures);

            OnLogItem?.Invoke(new CleanupLogItem
            {
                Message = message,
                Icon = failures > 0 ? "Warning24" : "Checkmark24",
                StatusColor = failures > 0 ? "Orange" : "Green"
            });
            return;
        }

        OnLogItem?.Invoke(new CleanupLogItem
        {
            Message = FormatSafe(Resources.Log_Clean ?? "{0}: limpo", label, itemsRemoved, CalculateSuccessPercent(itemsRemoved, skipped, failures)),
            Icon = failures > 0 ? "Warning24" : "Info24",
            StatusColor = failures > 0 ? "Orange" : "Gray"
        });
    }

    private static int CalculateSuccessPercent(int itemsRemoved, int skipped, int failures)
    {
        int totalProcessed = Math.Max(0, itemsRemoved + skipped + failures);
        if (totalProcessed == 0)
        {
            return 100;
        }

        return Math.Max(0, Math.Min(100, (int)Math.Round((double)itemsRemoved / totalProcessed * 100, MidpointRounding.AwayFromZero)));
    }

    private sealed record CleanupCategoryDefinition(string Key, string DisplayName, IReadOnlyList<CleanupTarget> Targets);

    private sealed record ScanResult(long Bytes, int Items);
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
