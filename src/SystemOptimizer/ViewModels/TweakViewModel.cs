using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using SystemOptimizer.Helpers;
using SystemOptimizer.Models;
using SystemOptimizer.Properties;
using Windows.UI;

namespace SystemOptimizer.ViewModels;

public partial class TweakViewModel : ObservableObject
{
    private readonly ITweak _tweak;

    public string Title => _tweak.Title;
    public string Description => _tweak.Description;
    public string Id => _tweak.Id;
    public TweakCategory Category => _tweak.Category;
    public ITweak Tweak => _tweak;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _statusText = $"○ {Resources.Status_Undefined}";

    [ObservableProperty]
    private SolidColorBrush _statusColor = new(Colors.Gray);

    [ObservableProperty]
    private Symbol _statusIcon = Symbol.Help;

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
        if (e.PropertyName != nameof(ITweak.Status)) return;
        try
        {
            App.DispatchToUi(UpdateStatusUI);
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
                    StatusIcon = Symbol.Accept;
                    StatusColor = Brush(0x0f, 0x7b, 0x0f);
                    break;
                case TweakStatus.Default:
                    StatusText = Resources.Status_Default;
                    StatusIcon = Symbol.Cancel;
                    StatusColor = Brush(0xc4, 0x2b, 0x1c);
                    break;
                case TweakStatus.Modified:
                    StatusText = Resources.Status_Modified;
                    StatusIcon = Symbol.Edit;
                    StatusColor = Brush(202, 80, 16);
                    break;
                default:
                    StatusText = Resources.Status_Unknown;
                    StatusIcon = Symbol.Help;
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

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(ColorHelper.FromArgb(255, r, g, b));
}
