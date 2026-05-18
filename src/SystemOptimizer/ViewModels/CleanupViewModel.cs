using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using SystemOptimizer.Models;
using SystemOptimizer.Services;

namespace SystemOptimizer.ViewModels;

public partial class CleanupViewModel : ObservableObject, IDisposable
{
    private readonly CleanupService _cleanupService;
    private readonly Action<CleanupLogItem> _cleanupLogHandler;
    private readonly Action<CleanupProgressInfo> _cleanupProgressHandler;
    private bool _disposed;

    public ObservableCollection<CleanupLogItem> CleanupLogs { get; } = [];

    [ObservableProperty]
    private int _cleanupProgressPercentage;

    [ObservableProperty]
    private string _cleanupProgressCategory = string.Empty;

    [ObservableProperty]
    private int _cleanupProcessedItems;

    public CleanupViewModel(CleanupService cleanupService)
    {
        _cleanupService = cleanupService;

        _cleanupLogHandler = item =>
        {
            Application.Current.Dispatcher.InvokeAsync(() => CleanupLogs.Add(item), DispatcherPriority.Background);
        };

        _cleanupProgressHandler = progress =>
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                CleanupProgressPercentage = Math.Clamp(progress.Percentage, 0, 100);
                CleanupProgressCategory = progress.CurrentCategory ?? string.Empty;
                CleanupProcessedItems = Math.Max(0, progress.ProcessedItems);
            }, DispatcherPriority.Background);
        };

        _cleanupService.OnLogItem += _cleanupLogHandler;
        _cleanupService.OnProgress += _cleanupProgressHandler;
    }

    public Task<IReadOnlyList<CleanupCategoryResult>> RunCleanupScanAsync(CleanupOptions options, CancellationToken cancellationToken)
        => _cleanupService.RunScanAsync(options, cancellationToken);

    public Task RunSelectedCleanupAsync(CleanupOptions options, CancellationToken cancellationToken)
        => _cleanupService.RunCleanupAsync(options, cancellationToken);

    public void Dispose()
    {
        if (_disposed) return;
        _cleanupService.OnLogItem -= _cleanupLogHandler;
        _cleanupService.OnProgress -= _cleanupProgressHandler;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
