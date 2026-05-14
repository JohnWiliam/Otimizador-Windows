using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SystemOptimizer.Helpers;
using SystemOptimizer.Models;
using SystemOptimizer.Properties; // Namespace dos resources

namespace SystemOptimizer.ViewModels;

public partial class TweakViewModel : ObservableObject
{
    private readonly ITweak _tweak;

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
    private Symbol _statusIcon = Symbol.Help;

    public ITweak Tweak => _tweak;

    public TweakViewModel(ITweak tweak)
    {
        _tweak = tweak;

        if (_tweak is INotifyPropertyChanged notifyTweak)
        {
            notifyTweak.PropertyChanged += Tweak_PropertyChanged;
        }

        // Inicializa UI com status atual (Seguro pois roda na thread de criação)
        UpdateStatusUI();
    }

    private void Tweak_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Proteção contra crashes vindos de threads secundárias
        try
        {
            if (e.PropertyName == nameof(ITweak.Status))
            {
                App.EnqueueOnUiThread(UpdateStatusUI);
            }
        }
        catch (Exception ex)
        {
            // Loga o erro mas não derruba o aplicativo
            Logger.Log($"Erro ao atualizar UI do Tweak {_tweak.Id}: {ex.Message}", "WARNING");
        }
    }

    /// <summary>
    /// Updates the status UI based on the underlying tweak status.
    /// </summary>
    public void UpdateStatusUI()
    {
        try
        {
            switch (_tweak.Status)
            {
                case TweakStatus.Optimized:
                    StatusText = Resources.Status_Optimized;
                    StatusIcon = Symbol.Accept;
                    StatusColor = new SolidColorBrush(Color.FromArgb(255, 0x0f, 0x7b, 0x0f)); // Verde Escuro
                    break;
                case TweakStatus.Default:
                    StatusText = Resources.Status_Default;
                    StatusIcon = Symbol.Cancel;
                    StatusColor = new SolidColorBrush(Color.FromArgb(255, 0xc4, 0x2b, 0x1c)); // Vermelho
                    break;
                case TweakStatus.Modified:
                    StatusText = Resources.Status_Modified;
                    StatusIcon = Symbol.Edit;
                    StatusColor = new SolidColorBrush(Color.FromArgb(255, 202, 80, 16)); // Laranja
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

    /// <summary>
    /// Refreshes the status of the tweak asynchronously.
    /// </summary>
    public async Task RefreshStatusAsync()
    {
         await Task.Run(_tweak.CheckStatus);
    }
}
