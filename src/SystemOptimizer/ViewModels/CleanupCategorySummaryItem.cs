using System;
using System.ComponentModel;
using Res = SystemOptimizer.Properties.Resources;

namespace SystemOptimizer.ViewModels;

public class CleanupCategorySummaryItem : INotifyPropertyChanged
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
    public bool ShouldDisplaySize
    {
        get => _shouldDisplaySize;
        set
        {
            _shouldDisplaySize = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShouldDisplaySize)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SizeLabel)));
        }
    }

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
