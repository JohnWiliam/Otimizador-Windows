using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Res = SystemOptimizer.Properties.Resources;
using SystemOptimizer.Services;
using SystemOptimizer.ViewModels;

namespace SystemOptimizer.Views.Pages;

public partial class CleanupPage : Page, INotifyPropertyChanged
{
    private readonly MainViewModel _viewModel;

    private bool _isOptionsExpanded = true;
    private bool _isBusyLocal;
    private bool _hasScanResults;
    private bool _isCleanupOperation;

    private bool _cleanTemp = true;
    private bool _cleanSystemTemp = true;
    private bool _cleanPrefetch = true;
    private bool _cleanWindowsUpdate = true;
    private bool _cleanBrowser = true;
    private bool _cleanDns = true;
    private bool _cleanRecycleBin;

    private CleanupRunSummary? _lastCleanupSummary;
    private TimeSpan _lastCleanupDuration;
    private CancellationTokenSource? _cleanupCts;

    private static readonly HashSet<string> CategoriesWithoutSize = new(StringComparer.OrdinalIgnoreCase) { "dns" };

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand AnalyzeCommand { get; }
    public ICommand CleanupSelectedCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand DeselectAllCommand { get; }
    public ICommand SelectRecommendedCommand { get; }

    public ObservableCollection<CleanupCategorySummaryItem> ScanResults { get; } = [];

    public CleanupPage(MainViewModel viewModel)
    {
        AnalyzeCommand = new AsyncRelayCommand(AnalyzeAsync, () => !IsBusyLocal);
        CleanupSelectedCommand = new AsyncRelayCommand(CleanupSelectedAsync, () => !IsBusyLocal && HasScanResults && SelectedCategoriesCount > 0);
        CancelCommand = new RelayCommand(CancelCurrentOperation, () => IsBusyLocal);
        SelectAllCommand = new RelayCommand(SelectAllCategories, () => HasScanResults);
        DeselectAllCommand = new RelayCommand(DeselectAllCategories, () => HasScanResults);
        SelectRecommendedCommand = new RelayCommand(SelectRecommendedCategories, () => HasScanResults);

        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        ScanResults.CollectionChanged += ScanResults_CollectionChanged;
        Loaded += CleanupPage_Loaded;
        Unloaded += CleanupPage_Unloaded;
    }

    public bool IsOptionsExpanded { get => _isOptionsExpanded; set { _isOptionsExpanded = value; OnPropertyChanged(); } }

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
            OnPropertyChanged(nameof(BusyStatusLabel));
            OnPropertyChanged(nameof(SummaryTitle));
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
            OnPropertyChanged(nameof(SelectedSummaryLabel));
            OnPropertyChanged(nameof(SummaryTitle));
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
    public bool CanCleanup => !IsBusyLocal && HasScanResults && SelectedCategoriesCount > 0;
    public Visibility CancelVisibility => IsBusyLocal ? Visibility.Visible : Visibility.Collapsed;
    public bool ShouldShowSummaryCard => IsBusyLocal || HasScanResults || HasLastCleanupResult;
    public bool HasLastCleanupResult => _lastCleanupSummary is not null;
    public string BusyStatusLabel => _isCleanupOperation ? Res.Cleanup_ProgressCleaning : Res.Cleanup_ProgressAnalyzing;
    public string SummaryTitle => HasLastCleanupResult ? Res.Cleanup_ResultTitle : Res.Cleanup_ScanSummaryTitle;
    public string CleanupProcessedItemsLabel => string.Format(Res.Cleanup_ProgressProcessedItems, _viewModel.CleanupProcessedItems);
    public int SelectedCategoriesCount => ScanResults.Count(x => x.IsSelected);
    public string TotalScanSizeLabel => FormatBytes(ScanResults.Where(x => x.ShouldDisplaySize).Sum(x => x.Bytes));
    public string TotalScanItemsLabel => ScanResults.Sum(x => x.Items).ToString("N0", CultureInfo.CurrentCulture);
    public string SelectedSummaryLabel => HasScanResults
        ? string.Format(Res.Cleanup_SummarySelectionStatus, SelectedCategoriesCount, ScanResults.Count)
        : Res.Cleanup_SummarySelectionEmpty;

    public string LastCleanupCategoriesLabel => (_lastCleanupSummary?.ProcessedCategories ?? 0).ToString("N0", CultureInfo.CurrentCulture);
    public string LastCleanupRemovedSizeLabel => FormatBytes(_lastCleanupSummary?.BytesRemoved ?? 0);
    public string LastCleanupRemovedItemsLabel => (_lastCleanupSummary?.ItemsRemoved ?? 0).ToString("N0", CultureInfo.CurrentCulture);
    public string LastCleanupIgnoredItemsLabel => (_lastCleanupSummary?.ItemsIgnored ?? 0).ToString("N0", CultureInfo.CurrentCulture);
    public string LastCleanupFailuresLabel => (_lastCleanupSummary?.Failures ?? 0).ToString("N0", CultureInfo.CurrentCulture);
    public string LastCleanupDurationLabel => _lastCleanupDuration.TotalSeconds < 1
        ? _lastCleanupDuration.TotalMilliseconds.ToString("N0", CultureInfo.CurrentCulture) + " ms"
        : _lastCleanupDuration.TotalSeconds.ToString("N1", CultureInfo.CurrentCulture) + " s";

    private async Task AnalyzeAsync()
    {
        if (IsBusyLocal)
            return;

        try
        {
            IsBusyLocal = true;
            _isCleanupOperation = false;
            IsOptionsExpanded = false;
            HasScanResults = false;
            _cleanupCts = new CancellationTokenSource();

            ScanResults.Clear();

            var options = BuildCleanupOptions();
            var results = await _viewModel.RunCleanupScanAsync(options, _cleanupCts.Token);

            foreach (var result in results)
                ScanResults.Add(CreateSummaryItem(result));

            HasScanResults = ScanResults.Any(result => result.Items > 0);
            if (HasScanResults)
                AnimateSummaryCardEntrance();
        }
        finally
        {
            _cleanupCts?.Dispose();
            _cleanupCts = null;
            IsBusyLocal = false;
            OnSelectionMetricsChanged();
        }
    }

    private async Task CleanupSelectedAsync()
    {
        if (IsBusyLocal || !HasScanResults)
            return;

        try
        {
            IsBusyLocal = true;
            _isCleanupOperation = true;
            _cleanupCts = new CancellationTokenSource();

            var selected = ScanResults.Where(x => x.IsSelected).Select(x => x.Key).ToHashSet();
            if (selected.Count == 0)
                return;

            var options = BuildCleanupOptions(selected);
            var timer = Stopwatch.StartNew();
            _lastCleanupSummary = await _viewModel.RunSelectedCleanupAsync(options, _cleanupCts.Token);
            timer.Stop();
            _lastCleanupDuration = timer.Elapsed;

            ScanResults.Clear();
            HasScanResults = false;
            OnCleanupResultChanged();
        }
        finally
        {
            _cleanupCts?.Dispose();
            _cleanupCts = null;
            IsBusyLocal = false;
            _isCleanupOperation = false;
            OnPropertyChanged(nameof(BusyStatusLabel));
            OnSelectionMetricsChanged();
        }
    }

    private void CancelCurrentOperation() => _cleanupCts?.Cancel();

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

    private static CleanupCategorySummaryItem CreateSummaryItem(CleanupCategoryResult result)
    {
        bool shouldDisplaySize = !CategoriesWithoutSize.Contains(result.Key);

        return new CleanupCategorySummaryItem
        {
            Key = result.Key,
            DisplayName = result.DisplayName,
            Bytes = result.Bytes,
            Items = result.Items,
            IsSelected = result.IsSelected,
            ShouldDisplaySize = shouldDisplaySize
        };
    }

    private void SelectAllCategories()
    {
        foreach (var category in ScanResults)
            category.IsSelected = true;

        OnSelectionMetricsChanged();
    }

    private void DeselectAllCategories()
    {
        foreach (var category in ScanResults)
            category.IsSelected = false;

        OnSelectionMetricsChanged();
    }

    private void SelectRecommendedCategories()
    {
        foreach (var category in ScanResults)
            category.IsSelected = category.Items > 0 && category.Key != "windows-update";

        OnSelectionMetricsChanged();
    }

    private void ScanResults_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (CleanupCategorySummaryItem item in e.NewItems)
                item.PropertyChanged += SummaryItem_PropertyChanged;

        if (e.OldItems != null)
            foreach (CleanupCategorySummaryItem item in e.OldItems)
                item.PropertyChanged -= SummaryItem_PropertyChanged;

        OnSelectionMetricsChanged();
    }

    private void SummaryItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CleanupCategorySummaryItem.IsSelected) or nameof(CleanupCategorySummaryItem.Items) or nameof(CleanupCategorySummaryItem.Bytes))
            OnSelectionMetricsChanged();
    }

    private void OnSelectionMetricsChanged()
    {
        OnPropertyChanged(nameof(SelectedCategoriesCount));
        OnPropertyChanged(nameof(TotalScanSizeLabel));
        OnPropertyChanged(nameof(TotalScanItemsLabel));
        OnPropertyChanged(nameof(SelectedSummaryLabel));
        OnPropertyChanged(nameof(CanCleanup));
        RefreshCommands();
    }

    private void OnCleanupResultChanged()
    {
        OnPropertyChanged(nameof(HasLastCleanupResult));
        OnPropertyChanged(nameof(LastCleanupCategoriesLabel));
        OnPropertyChanged(nameof(LastCleanupRemovedSizeLabel));
        OnPropertyChanged(nameof(LastCleanupRemovedItemsLabel));
        OnPropertyChanged(nameof(LastCleanupIgnoredItemsLabel));
        OnPropertyChanged(nameof(LastCleanupFailuresLabel));
        OnPropertyChanged(nameof(LastCleanupDurationLabel));
        OnPropertyChanged(nameof(ShouldShowSummaryCard));
        OnPropertyChanged(nameof(SummaryTitle));
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
            return Res.Cleanup_SizeZero;

        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        int order = 0;

        while (value >= 1024 && order < suffixes.Length - 1)
        {
            order++;
            value /= 1024;
        }

        return $"{value:N1} {suffixes[order]}";
    }

    private void RefreshCommands()
    {
        (AnalyzeCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (CleanupSelectedCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (CancelCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (SelectAllCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (DeselectAllCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (SelectRecommendedCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CleanupProcessedItems))
            OnPropertyChanged(nameof(CleanupProcessedItemsLabel));
    }

    private void CleanupPage_Loaded(object sender, RoutedEventArgs e)
    {
        AnimateCardOnLoad(OptionsCard, -10, 220);
        AnimateCardOnLoad(SummaryCard, 10, 260);
    }

    private void CleanupPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _cleanupCts?.Cancel();
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        ScanResults.CollectionChanged -= ScanResults_CollectionChanged;
        Loaded -= CleanupPage_Loaded;
        Unloaded -= CleanupPage_Unloaded;

        foreach (var item in ScanResults)
            item.PropertyChanged -= SummaryItem_PropertyChanged;
    }

    private static void AnimateCardOnLoad(UIElement target, double fromY, int durationMs)
    {
        target.Opacity = 0;
        target.RenderTransform = new TranslateTransform(0, fromY);

        var storyboard = new Storyboard();
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(durationMs)) { EasingFunction = ease };
        Storyboard.SetTarget(fade, target);
        Storyboard.SetTargetProperty(fade, new PropertyPath("Opacity"));

        var slide = new DoubleAnimation(fromY, 0, TimeSpan.FromMilliseconds(durationMs)) { EasingFunction = ease };
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
        var animation = new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease };

        transform.BeginAnimation(TranslateTransform.YProperty, animation);
        SummaryCard.BeginAnimation(OpacityProperty, new DoubleAnimation(0.6, 1, TimeSpan.FromMilliseconds(220)));
    }
}

public class CleanupCategorySummaryItem : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _shouldDisplaySize = true;
    private long _bytes;
    private int _items;

    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    public long Bytes
    {
        get => _bytes;
        set
        {
            _bytes = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HumanSize));
            OnPropertyChanged(nameof(SizeLabel));
        }
    }

    public int Items
    {
        get => _items;
        set
        {
            _items = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ItemsLabel));
        }
    }

    public string HumanSize => BytesToMb(Bytes);
    public string ItemsLabel => string.Format(Res.Cleanup_SummaryItemsLabel, Items);
    public string SizeLabel => ShouldDisplaySize ? HumanSize : Res.Cleanup_SizeNotApplicable;

    public bool ShouldDisplaySize
    {
        get => _shouldDisplaySize;
        set
        {
            _shouldDisplaySize = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SizeLabel));
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static string BytesToMb(long bytes)
    {
        if (bytes <= 0)
            return Res.Cleanup_SizeZero;

        return $"{Math.Round(bytes / 1024.0 / 1024.0, 2):N2} MB";
    }
}
