using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using SystemOptimizer.Services;
using SystemOptimizer.ViewModels;

namespace SystemOptimizer.Views.Pages;

public sealed partial class CleanupPage : Page, INotifyPropertyChanged
{
    private readonly MainViewModel _viewModel;
    private CancellationTokenSource? _cleanupCts;
    private bool _isBusyLocal;
    private bool _hasScanResults;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand AnalyzeCommand { get; }
    public ICommand CleanupSelectedCommand { get; }
    public ICommand CancelCommand { get; }

    public ObservableCollection<CleanupCategorySummaryItem> ScanResults { get; } = [];

    public CleanupPage()
    {
        _viewModel = (MainViewModel)App.Services.GetService(typeof(MainViewModel))!;
        AnalyzeCommand = new AsyncRelayCommand(AnalyzeAsync, () => !IsBusyLocal);
        CleanupSelectedCommand = new AsyncRelayCommand(CleanupSelectedAsync, () => !IsBusyLocal && HasScanResults);
        CancelCommand = new RelayCommand(CancelCurrentOperation, () => IsBusyLocal);

        InitializeComponent();
        DataContext = this;
    }

    public ObservableCollection<SystemOptimizer.Models.CleanupLogItem> CleanupLogs => _viewModel.CleanupLogs;

    public bool IsBusyLocal
    {
        get => _isBusyLocal;
        set
        {
            _isBusyLocal = value;
            OnPropertyChanged(nameof(IsBusyLocal));
            RefreshCommands();
        }
    }

    public bool HasScanResults
    {
        get => _hasScanResults;
        set
        {
            _hasScanResults = value;
            OnPropertyChanged(nameof(HasScanResults));
            RefreshCommands();
        }
    }

    public bool CleanTemp { get; set; } = true;
    public bool CleanSystemTemp { get; set; } = true;
    public bool CleanPrefetch { get; set; } = true;
    public bool CleanWindowsUpdate { get; set; } = true;
    public bool CleanBrowser { get; set; } = true;
    public bool CleanDns { get; set; } = true;
    public bool CleanRecycleBin { get; set; }

    private CleanupOptions CreateOptions() => new()
    {
        CleanUserTemp = CleanTemp,
        CleanSystemTemp = CleanSystemTemp,
        CleanPrefetch = CleanPrefetch,
        CleanWindowsUpdate = CleanWindowsUpdate,
        CleanBrowserCache = CleanBrowser,
        CleanDns = CleanDns,
        CleanRecycleBin = CleanRecycleBin
    };

    private async Task AnalyzeAsync()
    {
        await RunExclusiveAsync(async token =>
        {
            ScanResults.Clear();
            var results = await _viewModel.RunCleanupScanAsync(CreateOptions(), token);
            foreach (var result in results)
            {
                ScanResults.Add(CleanupCategorySummaryItem.From(result));
            }
            HasScanResults = ScanResults.Count > 0;
        });
    }

    private async Task CleanupSelectedAsync()
    {
        await RunExclusiveAsync(async token =>
        {
            await _viewModel.RunSelectedCleanupAsync(CreateOptions(), token);
        });
    }

    private async Task RunExclusiveAsync(Func<CancellationToken, Task> action)
    {
        if (IsBusyLocal) return;
        _cleanupCts = new CancellationTokenSource();
        IsBusyLocal = true;
        try
        {
            await action(_cleanupCts.Token);
        }
        catch (OperationCanceledException)
        {
            _viewModel.CleanupLogs.Add(new() { Message = "Operação cancelada pelo usuário." });
        }
        finally
        {
            _cleanupCts.Dispose();
            _cleanupCts = null;
            IsBusyLocal = false;
        }
    }

    private void CancelCurrentOperation() => _cleanupCts?.Cancel();

    private void RefreshCommands()
    {
        (AnalyzeCommand as IRelayCommand)?.NotifyCanExecuteChanged();
        (CleanupSelectedCommand as IRelayCommand)?.NotifyCanExecuteChanged();
        (CancelCommand as IRelayCommand)?.NotifyCanExecuteChanged();
    }

    private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class CleanupCategorySummaryItem
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public long Bytes { get; init; }
    public int Items { get; init; }
    public string Summary => $"{Math.Round(Bytes / 1024.0 / 1024.0, 2)} MB em {Items} item(ns)";

    public static CleanupCategorySummaryItem From(CleanupCategoryResult result) => new()
    {
        Key = result.Key,
        DisplayName = result.DisplayName,
        Bytes = result.Bytes,
        Items = result.Items
    };
}
