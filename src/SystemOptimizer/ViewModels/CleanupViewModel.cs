using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SystemOptimizer.Models;
using SystemOptimizer.Properties;
using SystemOptimizer.Services;

namespace SystemOptimizer.ViewModels;

public partial class CleanupViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;
    private CancellationTokenSource? _cleanupCts;
    private static readonly HashSet<string> CategoriesWithoutSize = new(StringComparer.OrdinalIgnoreCase) { "dns" };

    public ObservableCollection<CleanupCategorySummaryItem> ScanResults { get; } = [];
    public ObservableCollection<CleanupLogItem> CleanupLogs => _mainViewModel.CleanupLogs;

    [ObservableProperty] private bool _isOptionsExpanded = true;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasScanResults;
    [ObservableProperty] private bool _cleanTemp = true;
    [ObservableProperty] private bool _cleanSystemTemp = true;
    [ObservableProperty] private bool _cleanPrefetch = true;
    [ObservableProperty] private bool _cleanWindowsUpdate = true;
    [ObservableProperty] private bool _cleanBrowser = true;
    [ObservableProperty] private bool _cleanDns = true;
    [ObservableProperty] private bool _cleanRecycleBin;

    public CleanupViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        _mainViewModel.PropertyChanged += MainViewModelOnPropertyChanged;
        ScanResults.CollectionChanged += (_, _) => { OnPropertyChanged(nameof(TotalPotentialSizeLabel)); OnPropertyChanged(nameof(ScanResultCountLabel)); };
        CleanupLogs.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasLogs));
    }

    public bool CanAnalyze => !IsBusy;
    public bool CanCleanup => !IsBusy && HasScanResults;
    public bool HasLogs => CleanupLogs.Count > 0;
    public bool ShouldShowSummaryCard => IsBusy || HasScanResults;
    public int CleanupProgressPercentage => Math.Clamp(_mainViewModel.CleanupProgressPercentage, 0, 100);
    public string CleanupProgressCategory => string.IsNullOrWhiteSpace(_mainViewModel.CleanupProgressCategory) ? Resources.Cleanup_ReadyToAnalyze : _mainViewModel.CleanupProgressCategory;
    public string CleanupProcessedItemsLabel => string.Format(Resources.Cleanup_ProgressProcessedItems, _mainViewModel.CleanupProcessedItems);
    public string TotalPotentialSizeLabel => FormatBytes(ScanResults.Sum(result => result.Bytes));
    public string ScanResultCountLabel => HasScanResults ? string.Format(Resources.Cleanup_ScanResultCount, ScanResults.Count(result => result.Items > 0)) : Resources.Cleanup_WaitingForScan;

    [RelayCommand(CanExecute = nameof(CanAnalyze))]
    private async Task AnalyzeAsync()
    {
        var operationCts = BeginCleanupOperation();
        try
        {
            IsBusy = true;
            IsOptionsExpanded = false;
            HasScanResults = false;
            CleanupLogs.Clear();
            ScanResults.Clear();
            var results = await _mainViewModel.RunCleanupScanAsync(BuildCleanupOptions(), operationCts.Token);
            foreach (var result in results)
            {
                ScanResults.Add(new CleanupCategorySummaryItem { Key = result.Key, DisplayName = result.DisplayName, Bytes = result.Bytes, Items = result.Items, IsSelected = result.IsSelected, ShouldDisplaySize = !CategoriesWithoutSize.Contains(result.Key) });
            }
            HasScanResults = ScanResults.Any(r => r.Items > 0);
        }
        catch (OperationCanceledException) { CleanupLogs.Add(new CleanupLogItem { Message = Resources.Cleanup_FeedbackAnalyzeCanceled, Icon = "Dismiss24", StatusColor = "Orange", IsBold = true }); }
        catch (Exception ex) { CleanupLogs.Add(new CleanupLogItem { Message = string.Format(Resources.Cleanup_FeedbackAnalyzeError, ex.Message), Icon = "ErrorCircle24", StatusColor = "#E57373", IsBold = true }); }
        finally { EndCleanupOperation(operationCts); operationCts.Dispose(); IsBusy = false; NotifyCommandStates(); }
    }

    [RelayCommand(CanExecute = nameof(CanCleanup))]
    private async Task CleanupSelectedAsync()
    {
        var operationCts = BeginCleanupOperation();
        try
        {
            IsBusy = true;
            var selected = ScanResults.Where(x => x.IsSelected).Select(x => x.Key).ToHashSet();
            if (selected.Count == 0) { CleanupLogs.Add(new CleanupLogItem { Message = Resources.Cleanup_FeedbackSelectCategory, Icon = "Info24", StatusColor = "Orange" }); return; }
            await _mainViewModel.RunSelectedCleanupAsync(BuildCleanupOptions(selected), operationCts.Token);
            ScanResults.Clear();
            HasScanResults = false;
        }
        catch (OperationCanceledException) { CleanupLogs.Add(new CleanupLogItem { Message = Resources.Cleanup_FeedbackCleanupCanceled, Icon = "Dismiss24", StatusColor = "Orange", IsBold = true }); }
        catch (Exception ex) { CleanupLogs.Add(new CleanupLogItem { Message = string.Format(Resources.Cleanup_FeedbackCleanupError, ex.Message), Icon = "ErrorCircle24", StatusColor = "#E57373", IsBold = true }); }
        finally { EndCleanupOperation(operationCts); operationCts.Dispose(); IsBusy = false; NotifyCommandStates(); }
    }

    [RelayCommand(CanExecute = nameof(IsBusy))]
    private void Cancel() => Volatile.Read(ref _cleanupCts)?.Cancel();

    private CleanupOptions BuildCleanupOptions(ISet<string>? selectedCategories = null)
    {
        bool IsEnabled(string key, bool fallback) => selectedCategories == null ? fallback : selectedCategories.Contains(key);
        return new CleanupOptions { CleanUserTemp = IsEnabled("user-temp", CleanTemp), CleanSystemTemp = IsEnabled("system-temp", CleanSystemTemp), CleanPrefetch = IsEnabled("prefetch", CleanPrefetch), CleanWindowsUpdate = IsEnabled("windows-update", CleanWindowsUpdate), CleanBrowserCache = IsEnabled("browser-cache", CleanBrowser), CleanDns = IsEnabled("dns", CleanDns), CleanRecycleBin = IsEnabled("recycle-bin", CleanRecycleBin) };
    }
    private CancellationTokenSource BeginCleanupOperation() { var cts = new CancellationTokenSource(); Interlocked.Exchange(ref _cleanupCts, cts)?.Dispose(); return cts; }
    private void EndCleanupOperation(CancellationTokenSource operationCts) => Interlocked.CompareExchange(ref _cleanupCts, null, operationCts);
    private void MainViewModelOnPropertyChanged(object? s, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.CleanupProcessedItems) or nameof(MainViewModel.CleanupProgressCategory) or nameof(MainViewModel.CleanupProgressPercentage))
        {
            OnPropertyChanged(nameof(CleanupProcessedItemsLabel)); OnPropertyChanged(nameof(CleanupProgressCategory)); OnPropertyChanged(nameof(CleanupProgressPercentage));
        }
    }
    partial void OnIsBusyChanged(bool value) { OnPropertyChanged(nameof(CanAnalyze)); OnPropertyChanged(nameof(CanCleanup)); OnPropertyChanged(nameof(ShouldShowSummaryCard)); NotifyCommandStates(); }
    partial void OnHasScanResultsChanged(bool value) { OnPropertyChanged(nameof(CanCleanup)); OnPropertyChanged(nameof(ScanResultCountLabel)); OnPropertyChanged(nameof(ShouldShowSummaryCard)); NotifyCommandStates(); }
    private void NotifyCommandStates() { AnalyzeCommand.NotifyCanExecuteChanged(); CleanupSelectedCommand.NotifyCanExecuteChanged(); CancelCommand.NotifyCanExecuteChanged(); OnPropertyChanged(nameof(HasLogs)); }
    private static string FormatBytes(long bytes) { if (bytes <= 0) return "0 MB"; double size = bytes; string[] units = ["B","KB","MB","GB","TB"]; var i=0; while(size>=1024&&i<units.Length-1){size/=1024;i++;} return $"{Math.Round(size, i==0?0:2)} {units[i]}"; }
}
