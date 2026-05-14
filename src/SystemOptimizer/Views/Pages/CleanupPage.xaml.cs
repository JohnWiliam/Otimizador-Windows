using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using SystemOptimizer.Models;
using SystemOptimizer.Properties;
using SystemOptimizer.ViewModels;
using Res = SystemOptimizer.Properties.Resources;

namespace SystemOptimizer.Views.Pages;

public sealed class CleanupPage : Page, INotifyPropertyChanged
{
    private readonly MainViewModel _viewModel;
    private readonly StackPanel _summaryPanel = new() { Spacing = 8 };
    private readonly StackPanel _logPanel = new() { Spacing = 4 };
    private CancellationTokenSource? _cleanupCts;
    private bool _isBusyLocal;
    private bool _hasScanResults;

    public event PropertyChangedEventHandler? PropertyChanged;
    public IAsyncRelayCommand AnalyzeCommand { get; }
    public IAsyncRelayCommand CleanupSelectedCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public ObservableCollection<CleanupCategorySummaryItem> ScanResults { get; } = [];

    public bool CleanTemp { get; set; } = true;
    public bool CleanSystemTemp { get; set; } = true;
    public bool CleanPrefetch { get; set; } = true;
    public bool CleanWindowsUpdate { get; set; } = true;
    public bool CleanBrowser { get; set; } = true;
    public bool CleanDns { get; set; } = true;
    public bool CleanRecycleBin { get; set; }

    public bool IsBusyLocal
    {
        get => _isBusyLocal;
        private set { _isBusyLocal = value; OnPropertyChanged(); RefreshCommands(); }
    }

    public bool HasScanResults
    {
        get => _hasScanResults;
        private set { _hasScanResults = value; OnPropertyChanged(); RefreshCommands(); }
    }

    public CleanupPage()
    {
        _viewModel = App.Services.GetRequiredService<MainViewModel>();
        DataContext = _viewModel;
        AnalyzeCommand = new AsyncRelayCommand(AnalyzeAsync, () => !IsBusyLocal);
        CleanupSelectedCommand = new AsyncRelayCommand(CleanupSelectedAsync, () => !IsBusyLocal && HasScanResults);
        CancelCommand = new RelayCommand(CancelCurrentOperation, () => IsBusyLocal);
        Content = BuildContent();

        _viewModel.CleanupLogs.CollectionChanged += (_, _) => RenderLogs();
        ScanResults.CollectionChanged += (_, _) => RenderSummary();
    }

    private UIElement BuildContent()
    {
        var root = new ScrollViewer { Padding = new Thickness(28) };
        var stack = new StackPanel { Spacing = 16 };
        root.Content = stack;
        stack.Children.Add(new TextBlock { Text = Resources.Cleanup_Title, FontSize = 28, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        stack.Children.Add(CreateOptionsCard());
        stack.Children.Add(CreateActionsCard());
        stack.Children.Add(CreateSection(Resources.Cleanup_ScanSummaryTitle, _summaryPanel));
        stack.Children.Add(CreateSection(Resources.Cleanup_LogTitle, _logPanel));
        return root;
    }

    private UIElement CreateOptionsCard()
    {
        var panel = new StackPanel { Spacing = 8 };
        AddOption(panel, Res.Cleanup_OptionTemp, v => CleanTemp = v, CleanTemp);
        AddOption(panel, Res.Cleanup_OptionSystemTemp, v => CleanSystemTemp = v, CleanSystemTemp);
        AddOption(panel, Res.Cleanup_OptionPrefetch, v => CleanPrefetch = v, CleanPrefetch);
        AddOption(panel, Res.Cleanup_OptionWindowsUpdate, v => CleanWindowsUpdate = v, CleanWindowsUpdate);
        AddOption(panel, Res.Cleanup_OptionBrowser, v => CleanBrowser = v, CleanBrowser);
        AddOption(panel, Res.Cleanup_OptionDNS, v => CleanDns = v, CleanDns);
        AddOption(panel, Res.Cleanup_OptionRecycleBin, v => CleanRecycleBin = v, CleanRecycleBin);
        return CreateSection(Res.Cleanup_CacheTitle, panel);
    }

    private static void AddOption(Panel panel, string text, Action<bool> setter, bool value)
    {
        var check = new CheckBox { Content = text, IsChecked = value };
        check.Checked += (_, _) => setter(true);
        check.Unchecked += (_, _) => setter(false);
        panel.Children.Add(check);
    }

    private UIElement CreateActionsCard()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var analyze = new Button { Content = Res.Cleanup_ActionAnalyze, Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
        analyze.Click += (_, _) => AnalyzeCommand.Execute(null);
        var clean = new Button { Content = Res.Cleanup_ActionClean };
        clean.Click += (_, _) => CleanupSelectedCommand.Execute(null);
        var cancel = new Button { Content = Res.Cleanup_ActionCancel };
        cancel.Click += (_, _) => CancelCommand.Execute(null);
        panel.Children.Add(analyze);
        panel.Children.Add(clean);
        panel.Children.Add(cancel);
        return panel;
    }

    private static Border CreateSection(string title, UIElement content)
    {
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(content);
        return new Border
        {
            Padding = new Thickness(18),
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(48, 128, 128, 128)),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            Child = panel
        };
    }

    private async Task AnalyzeAsync()
    {
        if (IsBusyLocal) return;
        try
        {
            IsBusyLocal = true;
            HasScanResults = false;
            _cleanupCts = new CancellationTokenSource();
            _viewModel.CleanupLogs.Clear();
            ScanResults.Clear();
            var results = await _viewModel.RunCleanupScanAsync(BuildCleanupOptions(), _cleanupCts.Token);
            foreach (var result in results) ScanResults.Add(CreateSummaryItem(result));
            HasScanResults = ScanResults.Any(x => x.Items > 0);
            if (!HasScanResults) _viewModel.CleanupLogs.Add(new CleanupLogItem { Message = Res.Cleanup_ScanSummaryEmpty, Icon = "Info", StatusColor = "Gray" });
        }
        catch (OperationCanceledException)
        {
            _viewModel.CleanupLogs.Add(new CleanupLogItem { Message = Res.Cleanup_FeedbackAnalyzeCanceled, StatusColor = "Orange", IsBold = true });
        }
        catch (Exception ex)
        {
            _viewModel.CleanupLogs.Add(new CleanupLogItem { Message = string.Format(Res.Cleanup_FeedbackAnalyzeError, ex.Message), StatusColor = "Red", IsBold = true });
        }
        finally
        {
            _cleanupCts?.Dispose();
            _cleanupCts = null;
            IsBusyLocal = false;
        }
    }

    private async Task CleanupSelectedAsync()
    {
        if (IsBusyLocal || !HasScanResults) return;
        try
        {
            IsBusyLocal = true;
            _cleanupCts = new CancellationTokenSource();
            var selected = ScanResults.Where(x => x.IsSelected).Select(x => x.Key).ToHashSet();
            if (selected.Count == 0)
            {
                _viewModel.CleanupLogs.Add(new CleanupLogItem { Message = Res.Cleanup_FeedbackSelectCategory, StatusColor = "Orange" });
                return;
            }

            await _viewModel.RunSelectedCleanupAsync(BuildCleanupOptions(selected), _cleanupCts.Token);
            ScanResults.Clear();
            HasScanResults = false;
        }
        catch (OperationCanceledException)
        {
            _viewModel.CleanupLogs.Add(new CleanupLogItem { Message = Res.Cleanup_FeedbackCleanupCanceled, StatusColor = "Orange", IsBold = true });
        }
        catch (Exception ex)
        {
            _viewModel.CleanupLogs.Add(new CleanupLogItem { Message = string.Format(Res.Cleanup_FeedbackCleanupError, ex.Message), StatusColor = "Red", IsBold = true });
        }
        finally
        {
            _cleanupCts?.Dispose();
            _cleanupCts = null;
            IsBusyLocal = false;
        }
    }

    private void CancelCurrentOperation() => _cleanupCts?.Cancel();

    private CleanupOptions BuildCleanupOptions(ISet<string>? selected = null)
    {
        bool Enabled(string key, bool fallback) => selected is null ? fallback : selected.Contains(key);
        return new CleanupOptions
        {
            CleanUserTemp = Enabled("user-temp", CleanTemp),
            CleanSystemTemp = Enabled("system-temp", CleanSystemTemp),
            CleanPrefetch = Enabled("prefetch", CleanPrefetch),
            CleanWindowsUpdate = Enabled("windows-update", CleanWindowsUpdate),
            CleanBrowserCache = Enabled("browser-cache", CleanBrowser),
            CleanDns = Enabled("dns", CleanDns),
            CleanRecycleBin = Enabled("recycle-bin", CleanRecycleBin)
        };
    }

    private static CleanupCategorySummaryItem CreateSummaryItem(CleanupCategoryResult result) => new()
    {
        Key = result.Key,
        DisplayName = result.DisplayName,
        Bytes = result.Bytes,
        Items = result.Items,
        IsSelected = result.IsSelected,
        ShouldDisplaySize = !string.Equals(result.Key, "dns", StringComparison.OrdinalIgnoreCase)
    };

    private void RenderSummary()
    {
        _summaryPanel.Children.Clear();
        foreach (var item in ScanResults)
        {
            var row = new Grid { ColumnSpacing = 10 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var check = new CheckBox { IsChecked = item.IsSelected };
            check.Checked += (_, _) => item.IsSelected = true;
            check.Unchecked += (_, _) => item.IsSelected = false;
            var text = new TextBlock { Text = $"{item.DisplayName} • {item.ItemsLabel} • {item.SizeLabel}", TextWrapping = TextWrapping.WrapWholeWords };
            Grid.SetColumn(text, 1);
            row.Children.Add(check);
            row.Children.Add(text);
            _summaryPanel.Children.Add(row);
        }
    }

    private void RenderLogs()
    {
        _logPanel.Children.Clear();
        foreach (var item in _viewModel.CleanupLogs)
        {
            _logPanel.Children.Add(new TextBlock
            {
                Text = item.Message,
                TextWrapping = TextWrapping.WrapWholeWords,
                FontWeight = item.IsBold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal
            });
        }
    }

    private void RefreshCommands()
    {
        AnalyzeCommand.NotifyCanExecuteChanged();
        CleanupSelectedCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class CleanupCategorySummaryItem : INotifyPropertyChanged
{
    private bool _isSelected = true;
    private bool _shouldDisplaySize = true;
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public long Bytes { get; set; }
    public int Items { get; set; }
    public string HumanSize => $"{Math.Round(Bytes / 1024.0 / 1024.0, 2)} MB";
    public string ItemsLabel => string.Format(Res.Cleanup_SummaryItemsLabel, Items);
    public string SizeLabel => ShouldDisplaySize ? HumanSize : "—";
    public bool ShouldDisplaySize { get => _shouldDisplaySize; set { _shouldDisplaySize = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SizeLabel))); } }
    public bool IsSelected { get => _isSelected; set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}
