using Microsoft.Win32;
using System.Diagnostics;
using System.Threading;

namespace SystemOptimizer.Services;

public class SearchRegistryService
{
    // Registry paths
    private const string RegPolicy = @"Software\Policies\Microsoft\Windows\Explorer";
    private const string RegSearch = @"Software\Microsoft\Windows\CurrentVersion\Search";

    public enum OptimizationStatus
    {
        Optimized,
        NotOptimized,
        Unknown
    }

    // 1. DisableSearchBoxSuggestions (Policy)
    // Check: Value == 1 (Optimized)
    public OptimizationStatus CheckSearchBoxSuggestions()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegPolicy);
            if (key != null)
            {
                var val = key.GetValue("DisableSearchBoxSuggestions");
                if (val is int iVal && iVal == 1) return OptimizationStatus.Optimized;
            }
            return OptimizationStatus.NotOptimized;
        }
        catch
        {
            return OptimizationStatus.Unknown;
        }
    }

    public void ApplySearchBoxSuggestions()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegPolicy);
        key?.SetValue("DisableSearchBoxSuggestions", 1, RegistryValueKind.DWord);
    }

    public void RevertSearchBoxSuggestions()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegPolicy, true);
        if (key != null)
        {
            key.DeleteValue("DisableSearchBoxSuggestions", false);
        }
    }

    // 2. DisableCloudSearch
    // Check: Value == 1 (Optimized)
    public OptimizationStatus CheckCloudSearch()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegSearch);
            if (key != null)
            {
                var val = key.GetValue("DisableCloudSearch");
                if (val is int iVal && iVal == 1) return OptimizationStatus.Optimized;
            }
            return OptimizationStatus.NotOptimized;
        }
        catch
        {
            return OptimizationStatus.Unknown;
        }
    }

    public void ApplyCloudSearch()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegSearch);
        key?.SetValue("DisableCloudSearch", 1, RegistryValueKind.DWord);
    }

    public void RevertCloudSearch()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegSearch, true);
        if (key != null)
        {
            key.DeleteValue("DisableCloudSearch", false);
        }
    }

    // 3. BingSearchEnabled
    // Check: Value == 0 (Optimized)
    public OptimizationStatus CheckBingSearch()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegSearch);
            if (key != null)
            {
                var val = key.GetValue("BingSearchEnabled");
                if (val is int iVal && iVal == 0) return OptimizationStatus.Optimized;
            }
            return OptimizationStatus.NotOptimized;
        }
        catch
        {
            return OptimizationStatus.Unknown;
        }
    }

    public void ApplyBingSearch()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegSearch);
        key?.SetValue("BingSearchEnabled", 0, RegistryValueKind.DWord);
    }

    public void RevertBingSearch()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegSearch, true);
        if (key != null)
        {
            key.DeleteValue("BingSearchEnabled", false);
        }
    }

    public void RestartExplorer()
    {
        foreach (var process in Process.GetProcessesByName("explorer"))
        {
            try
            {
                process.Kill();
            }
            catch { /* Ignore if unable to kill */ }
        }
        
        // Allow a brief moment for the kill to complete
        Thread.Sleep(500);

        // Explicitly restart Explorer
        try
        {
             Process.Start("explorer.exe");
        }
        catch 
        {
            // If it fails, Windows usually restarts it anyway
        }
    }
}
