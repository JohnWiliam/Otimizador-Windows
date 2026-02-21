using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SystemOptimizer.Models;
using SystemOptimizer.Properties;

namespace SystemOptimizer.Services;

public class CleanupService
{
    private readonly CleanupExecutionEngine _executionEngine = new();

    public event Action<CleanupLogItem>? OnLogItem;

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
        OnLogItem?.Invoke(new CleanupLogItem { Message = Resources.Log_Starting, Icon = "Play24", StatusColor = "#0078D4", IsBold = true });

        var providers = BuildProviders(options);
        var totals = new CleanupResult { CategoryName = "TOTAL" };

        foreach (var provider in providers)
        {
            foreach (var target in provider.GetTargets())
            {
                CleanupResult result = await _executionEngine.ExecuteAsync(target);
                UpdateTotals(totals, result);
                EmitTargetLog(result);
            }
        }

        double totalMb = Math.Round(totals.BytesRemoved / 1024.0 / 1024.0, 2);
        OnLogItem?.Invoke(new CleanupLogItem
        {
            Message = string.Format(Resources.Log_Finished, totalMb),
            Icon = "CheckmarkCircle24",
            StatusColor = "#0078D4",
            IsBold = true
        });

        OnLogItem?.Invoke(new CleanupLogItem
        {
            Message = $"Resumo: removidos={totals.ItemsRemoved}, ignorados={totals.ItemsIgnored}, falhas={totals.Failures}",
            Icon = "Info24",
            StatusColor = totals.Failures > 0 ? "Orange" : "Green"
        });
    }

    private static List<ICleanupTargetProvider> BuildProviders(CleanupOptions options)
    {
        var providers = new List<ICleanupTargetProvider>();

        if (options.CleanWindowsUpdate)
            providers.Add(new WindowsUpdateCleanupTargetProvider());

        if (options.CleanUserTemp)
            providers.Add(new UserTempCleanupTargetProvider());

        if (options.CleanSystemTemp)
            providers.Add(new SystemTempCleanupTargetProvider());

        if (options.CleanPrefetch)
            providers.Add(new PrefetchCleanupTargetProvider());

        if (options.CleanBrowserCache)
            providers.Add(new BrowserCacheCleanupTargetProvider());

        if (options.CleanDns)
            providers.Add(new DnsCleanupTargetProvider());

        if (options.CleanRecycleBin)
            providers.Add(new RecycleBinCleanupTargetProvider());

        return providers;
    }

    private void EmitTargetLog(CleanupResult result)
    {
        double mb = Math.Round(result.BytesRemoved / 1024.0 / 1024.0, 2);
        string statusColor = result.Failures > 0 ? "Orange" : "Green";

        OnLogItem?.Invoke(new CleanupLogItem
        {
            Message = $"{result.CategoryName}: removidos={result.ItemsRemoved}, {mb} MB, ignorados={result.ItemsIgnored}, falhas={result.Failures}, duração={result.Duration.TotalMilliseconds:0}ms",
            Icon = result.Failures > 0 ? "Warning24" : "Checkmark24",
            StatusColor = statusColor
        });
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
