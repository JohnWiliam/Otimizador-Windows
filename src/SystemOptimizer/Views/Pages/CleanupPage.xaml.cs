using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SystemOptimizer.Models;
using SystemOptimizer.Properties;
using Res = SystemOptimizer.Properties.Resources;
using SystemOptimizer.Services;
using SystemOptimizer.ViewModels;
using Wpf.Ui.Controls;

namespace SystemOptimizer.Views.Pages;

public partial class CleanupPage : Page, INotifyPropertyChanged
{
    private readonly MainViewModel _viewModel;

    private bool _isOptionsExpanded = true;
    private bool _isBusyLocal;
    private bool _hasScanResults;

    private bool _cleanTemp = true;
    private bool _cleanSystemTemp = true;
    private bool _cleanPrefetch = true;
    private bool _cleanWindowsUpdate = true;
    private bool _cleanBrowser = true;
    private bool _cleanDns = true;
    private bool _cleanRecycleBin;

    private const int LogAnimationDelayMs = 180;

    private readonly Queue<CleanupLogItem> _pendingLogs = new();
    private CancellationTokenSource? _cleanupCts;
    private CancellationTokenSource? _logRenderCts;
    private Task? _logRenderTask;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand AnalyzeCommand { get; }
    public ICommand CleanupSelectedCommand { get; }
    public ICommand CancelCommand { get; }

    public ObservableCollection<CleanupCategorySummaryItem> ScanResults { get; } = [];

    public CleanupPage(MainViewModel viewModel)
    {
        AnalyzeCommand = new AsyncRelayCommand(AnalyzeAsync, () => !IsBusyLocal);
        CleanupSelectedCommand = new AsyncRelayCommand(CleanupSelectedAsync, () => !IsBusyLocal && HasScanResults);
        CancelCommand = new RelayCommand(CancelCurrentOperation, () => IsBusyLocal);

        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        _viewModel.CleanupLogs.CollectionChanged += CleanupLogs_CollectionChanged;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        Loaded += CleanupPage_Loaded;
    }

    public bool IsOptionsExpanded
    {
        get => _isOptionsExpanded;
        set { _isOptionsExpanded = value; OnPropertyChanged(); }
    }

    public bool IsBusyLocal
    {
        get => _isBusyLocal;
        set
        {
            _isBusyLocal = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanAnalyze));
            OnPropertyChanged(nameof(CanCleanup));
            OnPropertyChanged(nameof(CancelVisibility));
            OnPropertyChanged(nameof(ShouldShowSummaryCard));
            RefreshCommands();
        }
    }

    public bool HasScanResults
    {
        get => _hasScanResults;
        set
        {
            _hasScanResults = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanCleanup));
            OnPropertyChanged(nameof(ShouldShowSummaryCard));
            RefreshCommands();
        }
    }

    public bool CleanTemp { get => _cleanTemp; set { _cleanTemp = value; OnPropertyChanged(); } }
    public bool CleanSystemTemp { get => _cleanSystemTemp; set { _cleanSystemTemp = value; OnPropertyChanged(); } }
    public bool CleanPrefetch { get => _cleanPrefetch; set { _cleanPrefetch = value; OnPropertyChanged(); } }
    public bool CleanWindowsUpdate { get => _cleanWindowsUpdate; set { _cleanWindowsUpdate = value; OnPropertyChanged(); } }
    public bool CleanBrowser { get => _cleanBrowser; set { _cleanBrowser = value; OnPropertyChanged(); } }
    public bool CleanDns { get => _cleanDns; set { _cleanDns = value; OnPropertyChanged(); } }
    public bool CleanRecycleBin { get => _cleanRecycleBin; set { _cleanRecycleBin = value; OnPropertyChanged(); } }

    public bool CanAnalyze => !IsBusyLocal;
    public bool CanCleanup => !IsBusyLocal && HasScanResults;
    public bool HasLogs => _viewModel.CleanupLogs.Count > 0;
    public Visibility CancelVisibility => IsBusyLocal ? Visibility.Visible : Visibility.Collapsed;
    public bool ShouldShowSummaryCard => IsBusyLocal || HasScanResults;
    public string CleanupProcessedItemsLabel => string.Format(Res.Cleanup_ProgressProcessedItems, _viewModel.CleanupProcessedItems);

    private async Task AnalyzeAsync()
    {
        if (IsBusyLocal)
            return;

        try
        {
            IsBusyLocal = true;
            IsOptionsExpanded = false;
            HasScanResults = false;
            _cleanupCts = new CancellationTokenSource();

            Application.Current.Dispatcher.Invoke(() =>
            {
                _viewModel.CleanupLogs.Clear();
                ScanResults.Clear();
            });

            var options = BuildCleanupOptions();
            var results = await _viewModel.RunCleanupScanAsync(options, _cleanupCts.Token);

            foreach (var result in results)
            {
                ScanResults.Add(new CleanupCategorySummaryItem
                {
                    Key = result.Key,
                    DisplayName = result.DisplayName,
                    Bytes = result.Bytes,
                    Items = result.Items,
                    IsSelected = result.IsSelected
                });
            }

            HasScanResults = ScanResults.Any(result => result.Items > 0);

            if (HasScanResults)
            {
                AnimateSummaryCardEntrance();
            }
        }
        catch (OperationCanceledException)
        {
            _viewModel.CleanupLogs.Add(new CleanupLogItem { Message = Res.Cleanup_FeedbackAnalyzeCanceled, Icon = "Dismiss24", StatusColor = "Orange", IsBold = true });
        }
        catch (Exception ex)
        {
            _viewModel.CleanupLogs.Add(new CleanupLogItem { Message = string.Format(Res.Cleanup_FeedbackAnalyzeError, ex.Message), Icon = "ErrorCircle24", StatusColor = "#E57373", IsBold = true });
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
        if (IsBusyLocal || !HasScanResults)
            return;

        try
        {
            IsBusyLocal = true;
            _cleanupCts = new CancellationTokenSource();

            var selected = ScanResults.Where(x => x.IsSelected).Select(x => x.Key).ToHashSet();
            var options = BuildCleanupOptions(selected);

            if (selected.Count == 0)
            {
                _viewModel.CleanupLogs.Add(new CleanupLogItem { Message = Res.Cleanup_FeedbackSelectCategory, Icon = "Info24", StatusColor = "Orange" });
                return;
            }

            SmoothScrollToLogsCard();

            await _viewModel.RunSelectedCleanupAsync(options, _cleanupCts.Token);
            ScanResults.Clear();
            HasScanResults = false;
        }
        catch (OperationCanceledException)
        {
            _viewModel.CleanupLogs.Add(new CleanupLogItem { Message = Res.Cleanup_FeedbackCleanupCanceled, Icon = "Dismiss24", StatusColor = "Orange", IsBold = true });
        }
        catch (Exception ex)
        {
            _viewModel.CleanupLogs.Add(new CleanupLogItem { Message = string.Format(Res.Cleanup_FeedbackCleanupError, ex.Message), Icon = "ErrorCircle24", StatusColor = "#E57373", IsBold = true });
        }
        finally
        {
            _cleanupCts?.Dispose();
            _cleanupCts = null;
            IsBusyLocal = false;
        }
    }

    private void CancelCurrentOperation()
    {
        _cleanupCts?.Cancel();
    }

    private CleanupOptions BuildCleanupOptions(ISet<string>? selectedCategories = null)
    {
        bool IsEnabled(string key, bool fallback) => selectedCategories == null ? fallback : selectedCategories.Contains(key);

        return new CleanupOptions
        {
            CleanUserTemp = IsEnabled("user-temp", CleanTemp),
            CleanSystemTemp = IsEnabled("system-temp", CleanSystemTemp),
            CleanPrefetch = IsEnabled("prefetch", CleanPrefetch),
            CleanWindowsUpdate = IsEnabled("windows-update", CleanWindowsUpdate),
            CleanBrowserCache = IsEnabled("browser-cache", CleanBrowser),
            CleanDns = IsEnabled("dns", CleanDns),
            CleanRecycleBin = IsEnabled("recycle-bin", CleanRecycleBin)
        };
    }

    private void RefreshCommands()
    {
        (AnalyzeCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (CleanupSelectedCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (CancelCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }


    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CleanupProcessedItems))
        {
            OnPropertyChanged(nameof(CleanupProcessedItemsLabel));
        }
    }

    private void CleanupLogs_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            _pendingLogs.Clear();
            _logRenderCts?.Cancel();
            LogOutput.Document.Blocks.Clear();
        }
        else if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
        {
            foreach (CleanupLogItem item in e.NewItems)
            {
                _pendingLogs.Enqueue(item);
            }

            StartLogRenderLoop();
        }

        OnPropertyChanged(nameof(HasLogs));
    }

    private void StartLogRenderLoop()
    {
        if (_logRenderTask is { IsCompleted: false })
        {
            return;
        }

        _logRenderCts?.Dispose();
        _logRenderCts = new CancellationTokenSource();
        _logRenderTask = RenderQueuedLogsAsync(_logRenderCts.Token);
    }

    private async Task RenderQueuedLogsAsync(CancellationToken token)
    {
        try
        {
            while (_pendingLogs.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                var item = _pendingLogs.Dequeue();

                await Dispatcher.InvokeAsync(() =>
                {
                    AnimateLogsCardPulse();
                    AppendLog(item);
                });

                await Task.Delay(LogAnimationDelayMs, token);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void AppendLog(CleanupLogItem item)
    {
        var paragraph = new Paragraph
        {
            FontFamily = new FontFamily("Segoe UI"),
            TextAlignment = TextAlignment.Left,
            Margin = new Thickness(0, 0, 0, 4),
            LineHeight = 20
        };

        Brush statusBrush = GetHarmonicBrush(item.StatusColor, item.Message);

        SymbolRegular symbol = SymbolRegular.Info24;
        if (!Enum.TryParse(item.Icon, out SymbolRegular parsedSymbol))
        {
            string msgLower = item.Message.ToLower();

            if (msgLower.Contains("concluída") || msgLower.Contains("finished") || msgLower.Contains("sucesso") || msgLower.Contains("success") || msgLower.Contains("removidos") || msgLower.Contains("removed"))
                symbol = SymbolRegular.Checkmark24;
            else if (msgLower.Contains("erro") || msgLower.Contains("error") || msgLower.Contains("fail"))
                symbol = SymbolRegular.DismissCircle24;
            else if (msgLower.Contains("lixeira") || msgLower.Contains("bin") || msgLower.Contains("trash") || msgLower.Contains("delete"))
                symbol = SymbolRegular.Delete24;
            else if (msgLower.Contains("update"))
                symbol = SymbolRegular.ArrowSync24;
            else
                symbol = SymbolRegular.Info24;
        }
        else
        {
            symbol = parsedSymbol;
        }

        var icon = new SymbolIcon
        {
            Symbol = symbol,
            FontSize = 16,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = statusBrush,
            Margin = new Thickness(0, 0, 0, -2)
        };

        var iconContainer = new InlineUIContainer(icon)
        {
            BaselineAlignment = BaselineAlignment.Center
        };
        paragraph.Inlines.Add(iconContainer);
        paragraph.Inlines.Add(new Run("  "));

        string processedMessage = item.Message?.Replace("\\n", Environment.NewLine) ?? string.Empty;

        var run = new Run(processedMessage)
        {
            BaselineAlignment = BaselineAlignment.Center,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
            Foreground = statusBrush
        };

        if (item.IsBold)
        {
            run.FontWeight = FontWeights.SemiBold;
        }

        paragraph.Inlines.Add(run);

        LogOutput.Document.Blocks.Add(paragraph);
        LogOutput.ScrollToEnd();
    }

    private void SmoothScrollToLogsCard()
    {
        AnimateCardOnLoad(LogsCard, fromY: 22, durationMs: 420);

        Dispatcher.BeginInvoke(() =>
        {
            LogsCard.BringIntoView();
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private static Brush GetHarmonicBrush(string statusColor, string message)
    {
        string msg = message?.ToLower() ?? "";

        if (!string.IsNullOrEmpty(statusColor) && statusColor.StartsWith("#"))
        {
            try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(statusColor)); } catch { }
        }

        if (msg.Contains("update") || msg.Contains("serviço") || msg.Contains("service"))
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64B5F6"));

        if (msg.Contains("temp") || msg.Contains("lixeira") || msg.Contains("bin") || msg.Contains("cache") || msg.Contains("prefetch") || msg.Contains("removed") || msg.Contains("removidos"))
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#81C784"));

        if (msg.Contains("vazio") || msg.Contains("limpo") || msg.Contains("clean") || msg.Contains("empty"))
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B0BEC5"));

        if (msg.Contains("chrome") || msg.Contains("edge") || msg.Contains("firefox") || msg.Contains("browser") || msg.Contains("navegadores") || msg.Contains("shader"))
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD54F"));

        if (msg.Contains("dns") || msg.Contains("rede") || msg.Contains("network"))
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9575CD"));

        if (msg.Contains("concluída") || msg.Contains("finished") || msg.Contains("sucesso") || msg.Contains("success") || statusColor == "Green")
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4DB6AC"));

        if (msg.Contains("erro") || msg.Contains("error") || msg.Contains("fail") || msg.Contains("negado") || msg.Contains("denied"))
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E57373"));

        if (msg.Contains("iniciando") || msg.Contains("starting") || msg.Contains("parados") || msg.Contains("stopped"))
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4FC3F7"));

        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0"));
    }

    private void CleanupPage_Loaded(object sender, RoutedEventArgs e)
    {
        AnimateCardOnLoad(OptionsCard, fromY: -10, durationMs: 220);
        AnimateCardOnLoad(SummaryCard, fromY: 10, durationMs: 260);
        AnimateCardOnLoad(LogsCard, fromY: 14, durationMs: 300);
    }

    private static void AnimateCardOnLoad(UIElement target, double fromY, int durationMs)
    {
        target.Opacity = 0;
        target.RenderTransform = new TranslateTransform(0, fromY);

        var storyboard = new Storyboard();
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(durationMs))
        {
            EasingFunction = ease
        };
        Storyboard.SetTarget(fade, target);
        Storyboard.SetTargetProperty(fade, new PropertyPath("Opacity"));

        var slide = new DoubleAnimation(fromY, 0, TimeSpan.FromMilliseconds(durationMs))
        {
            EasingFunction = ease
        };
        Storyboard.SetTarget(slide, target);
        Storyboard.SetTargetProperty(slide, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));

        storyboard.Children.Add(fade);
        storyboard.Children.Add(slide);
        storyboard.Begin();
    }

    private void AnimateSummaryCardEntrance()
    {
        var transform = SummaryCard.RenderTransform as TranslateTransform ?? new TranslateTransform();
        SummaryCard.RenderTransform = transform;

        var ease = new QuinticEase { EasingMode = EasingMode.EaseOut };
        var animation = new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = ease
        };

        transform.BeginAnimation(TranslateTransform.YProperty, animation);
        SummaryCard.BeginAnimation(OpacityProperty, new DoubleAnimation(0.6, 1, TimeSpan.FromMilliseconds(220)));
    }

    private void AnimateLogsCardPulse()
    {
        var animation = new DoubleAnimation(1, 0.93, TimeSpan.FromMilliseconds(120))
        {
            AutoReverse = true,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        LogsCard.BeginAnimation(OpacityProperty, animation);
    }
}

public class CleanupCategorySummaryItem : INotifyPropertyChanged
{
    private bool _isSelected = true;

    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public long Bytes { get; set; }
    public int Items { get; set; }
    public string HumanSize => $"{Math.Round(Bytes / 1024.0 / 1024.0, 2)} MB";
    public string ItemsLabel => string.Format(Res.Cleanup_SummaryItemsLabel, Items);

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
