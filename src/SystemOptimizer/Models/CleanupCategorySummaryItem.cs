using System;
using System.ComponentModel;
using SystemOptimizer.Properties;

namespace SystemOptimizer.Models;

public class CleanupCategorySummaryItem : INotifyPropertyChanged
{
    private bool _isSelected = true;
    private bool _shouldDisplaySize = true;

    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public long Bytes { get; set; }
    public int Items { get; set; }
    public string HumanSize => $"{Math.Round(Bytes / 1024.0 / 1024.0, 2)} MB";
    public string ItemsLabel => string.Format(Resources.Cleanup_SummaryItemsLabel, Items);
    public string SizeLabel => ShouldDisplaySize ? HumanSize : "—";

    public bool ShouldDisplaySize
    {
        get => _shouldDisplaySize;
        set
        {
            if (_shouldDisplaySize == value)
            {
                return;
            }

            _shouldDisplaySize = value;
            OnPropertyChanged(nameof(ShouldDisplaySize));
            OnPropertyChanged(nameof(SizeLabel));
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged(nameof(IsSelected));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
