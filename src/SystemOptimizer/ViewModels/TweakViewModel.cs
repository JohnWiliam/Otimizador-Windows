using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.UI;
using SystemOptimizer.Helpers;
using SystemOptimizer.Models;
using SystemOptimizer.Properties;

namespace SystemOptimizer.ViewModels;

public partial class TweakViewModel : ObservableObject, IDisposable
{
    private readonly ITweak _tweak;
    private bool _disposed;

    public string Title => _tweak.Title;
    public string Description => _tweak.Description;
    public string Id => _tweak.Id;
    public TweakCategory Category => _tweak.Category;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _statusText = $"○ {Resources.Status_Undefined}";

    [ObservableProperty]
    private SolidColorBrush _statusColor = new(Colors.Gray);

    [ObservableProperty]
    private string _statusGlyph = "";

    public ITweak Tweak => _tweak;
    public string StatusDisplay => $"{StatusGlyph} {StatusText}";

    partial void OnStatusTextChanged(string value) => OnPropertyChanged(nameof(StatusDisplay));
    partial void OnStatusGlyphChanged(string value) => OnPropertyChanged(nameof(StatusDisplay));

    public TweakViewModel(ITweak tweak)
    {
        _tweak = tweak;
        if (_tweak is INotifyPropertyChanged notifyTweak)
        {
            notifyTweak.PropertyChanged += Tweak_PropertyChanged;
        }

        UpdateStatusUI();
    }

    private void Tweak_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_disposed || e.PropertyName != nameof(ITweak.Status)) return;

        try
        {
            var dispatcher = App.UiDispatcherQueue;
            if (dispatcher is null || dispatcher.HasThreadAccess) UpdateStatusUI();
            else dispatcher.TryEnqueue(UpdateStatusUI);
        }
        catch (Exception ex)
        {
            Logger.Log($"Erro ao atualizar UI do Tweak {_tweak.Id}: {ex.Message}", "WARNING");
        }
    }

    public void UpdateStatusUI()
    {
        try
        {
            switch (_tweak.Status)
            {
                case TweakStatus.Optimized:
                    StatusText = Resources.Status_Optimized;
                    StatusGlyph = "";
                    StatusColor = new SolidColorBrush(Color.FromArgb(255, 15, 123, 15));
                    break;
                case TweakStatus.Default:
                    StatusText = Resources.Status_Default;
                    StatusGlyph = "";
                    StatusColor = new SolidColorBrush(Color.FromArgb(255, 196, 43, 28));
                    break;
                case TweakStatus.Modified:
                    StatusText = Resources.Status_Modified;
                    StatusGlyph = "";
                    StatusColor = new SolidColorBrush(Color.FromArgb(255, 202, 80, 16));
                    break;
                default:
                    StatusText = Resources.Status_Unknown;
                    StatusGlyph = "";
                    StatusColor = new SolidColorBrush(Colors.Gray);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Erro interno no UpdateStatusUI ({_tweak.Id}): {ex.Message}", "ERROR");
        }
    }

    public async Task RefreshStatusAsync() => await Task.Run(_tweak.CheckStatus);

    public void Dispose()
    {
        if (_disposed) return;
        if (_tweak is INotifyPropertyChanged notifyTweak)
        {
            notifyTweak.PropertyChanged -= Tweak_PropertyChanged;
        }
        _disposed = true;
    }
}
