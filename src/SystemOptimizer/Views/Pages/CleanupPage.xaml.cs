using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SystemOptimizer.Models;
using SystemOptimizer.ViewModels;
using Wpf.Ui.Controls;

namespace SystemOptimizer.Views.Pages;

public partial class CleanupPage : Page
{
    private readonly CleanupViewModel _viewModel;

    public CleanupPage(CleanupViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += (_, _) => _viewModel.CleanupLogs.CollectionChanged += CleanupLogs_CollectionChanged;
        Unloaded += (_, _) => _viewModel.CleanupLogs.CollectionChanged -= CleanupLogs_CollectionChanged;
    }

    private void CleanupLogs_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            LogOutput.Document.Blocks.Clear();
            return;
        }

        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
        {
            foreach (CleanupLogItem item in e.NewItems)
            {
                AppendLog(item);
            }
        }
    }

    private void AppendLog(CleanupLogItem item)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 4), LineHeight = 20 };
        var icon = new SymbolIcon { Symbol = Enum.TryParse(item.Icon, out SymbolRegular p) ? p : SymbolRegular.Info24, FontSize = 16, Foreground = Brushes.LightGray };
        paragraph.Inlines.Add(new InlineUIContainer(icon));
        paragraph.Inlines.Add(new Run("  " + (item.Message ?? string.Empty).Replace("\\n", Environment.NewLine)) { FontSize = 13, FontWeight = item.IsBold ? FontWeights.SemiBold : FontWeights.Normal });
        LogOutput.Document.Blocks.Add(paragraph);
        LogOutput.ScrollToEnd();
    }
}

public class CleanupCategorySummaryItem : INotifyPropertyChanged
{
    private bool _isSelected = true;
    private bool _shouldDisplaySize = true;
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public long Bytes { get; set; }
    public int Items { get; set; }
    public string HumanSize => $"{Math.Round(Bytes / 1024.0 / 1024.0, 2)} MB";
    public string ItemsLabel => string.Format(SystemOptimizer.Properties.Resources.Cleanup_SummaryItemsLabel, Items);
    public string SizeLabel => ShouldDisplaySize ? HumanSize : "—";
    public bool ShouldDisplaySize { get => _shouldDisplaySize; set { _shouldDisplaySize = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShouldDisplaySize))); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SizeLabel))); } }
    public bool IsSelected { get => _isSelected; set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}
