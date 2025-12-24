using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Media;
using SystemOptimizer.Helpers;
using SystemOptimizer.Models;
using SystemOptimizer.Properties; // Namespace dos resources
using Wpf.Ui.Controls;

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
    private SolidColorBrush _statusColor = Brushes.Gray;

    [ObservableProperty]
    private SymbolRegular _statusIcon = SymbolRegular.QuestionCircle24;

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
                var app = System.Windows.Application.Current;

                // Se estivermos numa thread secundária, usamos o Dispatcher
                if (app != null && !app.Dispatcher.CheckAccess())
                {
                    app.Dispatcher.Invoke(UpdateStatusUI);
                }
                else
                {
                    // Se já estamos na UI Thread ou o App não está disponível, chamamos direto
                    UpdateStatusUI();
                }
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
                    StatusIcon = SymbolRegular.CheckmarkCircle24;
                    StatusColor = new SolidColorBrush(Color.FromRgb(0x0f, 0x7b, 0x0f)); // Verde Escuro
                    break;
                case TweakStatus.Default:
                    StatusText = Resources.Status_Default;
                    StatusIcon = SymbolRegular.DismissCircle24;
                    StatusColor = new SolidColorBrush(Color.FromRgb(0xc4, 0x2b, 0x1c)); // Vermelho
                    break;
                case TweakStatus.Modified:
                    StatusText = Resources.Status_Modified;
                    StatusIcon = SymbolRegular.Edit24;
                    StatusColor = new SolidColorBrush(Color.FromRgb(202, 80, 16)); // Laranja
                    break;
                default:
                    StatusText = Resources.Status_Unknown;
                    StatusIcon = SymbolRegular.QuestionCircle24;
                    StatusColor = Brushes.Gray;
                    break;
            }

            // Proteção extra ao congelar o pincel
            if (StatusColor.CanFreeze)
            {
                StatusColor.Freeze();
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
