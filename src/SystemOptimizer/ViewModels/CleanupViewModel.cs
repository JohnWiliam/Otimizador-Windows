using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using SystemOptimizer.Models;
using SystemOptimizer.Properties;
using SystemOptimizer.Services;

namespace SystemOptimizer.ViewModels;

public partial class CleanupViewModel : ObservableObject
{
    private readonly CleanupService _cleanupService;
    private CancellationTokenSource? _cleanupCts;
    private static readonly HashSet<string> CategoriesWithoutSize = new(StringComparer.OrdinalIgnoreCase) { "dns" };

    public ObservableCollection<CleanupLogItem> CleanupLogs { get; } = [];
    public ObservableCollection<CleanupCategorySummaryItem> ScanResults { get; } = [];

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
    [ObservableProperty] private int _cleanupProgressPercentage;
    [ObservableProperty] private string _cleanupProgressCategory = string.Empty;
    [ObservableProperty] private int _cleanupProcessedItems;

    public CleanupViewModel(CleanupService cleanupService)
    {
        _cleanupService = cleanupService;
        _cleanupService.OnLogItem += OnLogItem;
        _cleanupService.OnProgress += OnProgress;
    }

    partial void OnIsBusyChanged(bool value)
    {
        AnalyzeCommand.NotifyCanExecuteChanged(); CleanupSelectedCommand.NotifyCanExecuteChanged(); CancelCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanAnalyze)); OnPropertyChanged(nameof(CanCleanup)); OnPropertyChanged(nameof(CancelVisibility)); OnPropertyChanged(nameof(ShouldShowSummaryCard));
    }
    partial void OnHasScanResultsChanged(bool value)
    {
        CleanupSelectedCommand.NotifyCanExecuteChanged(); OnPropertyChanged(nameof(CanCleanup)); OnPropertyChanged(nameof(ShouldShowSummaryCard)); OnPropertyChanged(nameof(ScanResultCountLabel));
    }

    public bool CanAnalyze => !IsBusy;
    public bool CanCleanup => !IsBusy && HasScanResults;
    public bool HasLogs => CleanupLogs.Count > 0;
    public Visibility CancelVisibility => IsBusy ? Visibility.Visible : Visibility.Collapsed;
    public bool ShouldShowSummaryCard => IsBusy || HasScanResults;
    public string CleanupProcessedItemsLabel => string.Format(Resources.Cleanup_ProgressProcessedItems, CleanupProcessedItems);
    public string CurrentCleanupProgressCategory => string.IsNullOrWhiteSpace(CleanupProgressCategory) ? Resources.Cleanup_ReadyToAnalyze : CleanupProgressCategory;
    public string TotalPotentialSizeLabel => FormatBytes(ScanResults.Sum(result => result.Bytes));
    public string ScanResultCountLabel => HasScanResults ? string.Format(Resources.Cleanup_ScanResultCount, ScanResults.Count(r => r.Items > 0)) : Resources.Cleanup_WaitingForScan;

    [RelayCommand(CanExecute = nameof(CanAnalyze))]
    private async Task AnalyzeAsync()
    {
        var cts = Begin();
        try { IsBusy = true; IsOptionsExpanded = false; HasScanResults = false; CleanupLogs.Clear(); ClearScanResults();
            var results = await _cleanupService.RunScanAsync(BuildCleanupOptions(), cts.Token);
            foreach (var r in results) ScanResults.Add(new CleanupCategorySummaryItem { Key=r.Key,DisplayName=r.DisplayName,Bytes=r.Bytes,Items=r.Items,IsSelected=r.IsSelected,ShouldDisplaySize=!CategoriesWithoutSize.Contains(r.Key)});
            HasScanResults = ScanResults.Any(x=>x.Items>0); if(!HasScanResults) CleanupLogs.Add(new CleanupLogItem{Message=Resources.Cleanup_ScanSummaryEmpty,Icon="Info24",StatusColor="Gray"});
        } catch (OperationCanceledException){ CleanupLogs.Add(new CleanupLogItem{Message=Resources.Cleanup_FeedbackAnalyzeCanceled,Icon="Dismiss24",StatusColor="Orange",IsBold=true}); }
          catch (Exception ex){ CleanupLogs.Add(new CleanupLogItem{Message=string.Format(Resources.Cleanup_FeedbackAnalyzeError,ex.Message),Icon="ErrorCircle24",StatusColor="#E57373",IsBold=true}); }
        finally { End(cts); cts.Dispose(); IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanCleanup))]
    private async Task CleanupSelectedAsync()
    {
        var cts = Begin();
        try { IsBusy = true; var selected = ScanResults.Where(x=>x.IsSelected).Select(x=>x.Key).ToHashSet(); if(selected.Count==0){ CleanupLogs.Add(new CleanupLogItem{Message=Resources.Cleanup_FeedbackSelectCategory,Icon="Info24",StatusColor="Orange"}); return; }
            await _cleanupService.RunCleanupAsync(BuildCleanupOptions(selected), cts.Token); ClearScanResults(); HasScanResults = false;
        } catch (OperationCanceledException){ CleanupLogs.Add(new CleanupLogItem{Message=Resources.Cleanup_FeedbackCleanupCanceled,Icon="Dismiss24",StatusColor="Orange",IsBold=true}); }
          catch (Exception ex){ CleanupLogs.Add(new CleanupLogItem{Message=string.Format(Resources.Cleanup_FeedbackCleanupError,ex.Message),Icon="ErrorCircle24",StatusColor="#E57373",IsBold=true}); }
        finally { End(cts); cts.Dispose(); IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(IsBusy))] private void Cancel() => Volatile.Read(ref _cleanupCts)?.Cancel();
    private void OnLogItem(CleanupLogItem item) => Application.Current.Dispatcher.InvokeAsync(() => CleanupLogs.Add(item));
    private void OnProgress(CleanupProgressInfo p) => Application.Current.Dispatcher.InvokeAsync(() => { CleanupProgressPercentage = Math.Clamp(p.Percentage,0,100); CleanupProgressCategory = p.CurrentCategory; CleanupProcessedItems = p.ProcessedItems; OnPropertyChanged(nameof(CleanupProcessedItemsLabel)); OnPropertyChanged(nameof(CurrentCleanupProgressCategory)); });

    private CleanupOptions BuildCleanupOptions(ISet<string>? selected = null){ bool IsEnabled(string k, bool f)=> selected==null?f:selected.Contains(k); return new CleanupOptions{CleanUserTemp=IsEnabled("user-temp",CleanTemp),CleanSystemTemp=IsEnabled("system-temp",CleanSystemTemp),CleanPrefetch=IsEnabled("prefetch",CleanPrefetch),CleanWindowsUpdate=IsEnabled("windows-update",CleanWindowsUpdate),CleanBrowserCache=IsEnabled("browser-cache",CleanBrowser),CleanDns=IsEnabled("dns",CleanDns),CleanRecycleBin=IsEnabled("recycle-bin",CleanRecycleBin)}; }
    private CancellationTokenSource Begin(){ var cts=new CancellationTokenSource(); Interlocked.Exchange(ref _cleanupCts,cts)?.Dispose(); return cts; }
    private void End(CancellationTokenSource cts){ Interlocked.CompareExchange(ref _cleanupCts,null,cts); }
    private void ClearScanResults(){ ScanResults.Clear(); OnPropertyChanged(nameof(TotalPotentialSizeLabel)); OnPropertyChanged(nameof(ScanResultCountLabel)); }
    private static string FormatBytes(long bytes){ if(bytes<=0)return "0 MB"; double size=bytes; string[] units=["B","KB","MB","GB","TB"]; int i=0; while(size>=1024&&i<units.Length-1){size/=1024;i++;} return $"{Math.Round(size, i==0?0:2)} {units[i]}"; }
}
