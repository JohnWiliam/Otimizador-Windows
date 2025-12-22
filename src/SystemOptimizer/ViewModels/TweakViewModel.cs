using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using SystemOptimizer.Models;
using Wpf.Ui.Common;

namespace SystemOptimizer.ViewModels
{
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
        private string _statusText = "○ INDEFINIDO";

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

            // Initialize UI with current status
            UpdateStatusUI();
        }

        private void Tweak_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ITweak.Status))
            {
                // Ensure UI update happens on UI thread
                if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
                {
                    Application.Current.Dispatcher.Invoke(UpdateStatusUI);
                }
                else
                {
                    UpdateStatusUI();
                }
            }
        }

        /// <summary>
        /// Updates the status UI based on the underlying tweak status.
        /// This method is fast as it only reads the property.
        /// </summary>
        public void UpdateStatusUI()
        {
            switch (_tweak.Status)
            {
                case TweakStatus.Optimized:
                    StatusText = "Otimizado";
                    StatusIcon = SymbolRegular.CheckmarkCircle24;
                    StatusColor = new SolidColorBrush(Color.FromRgb(0x0f, 0x7b, 0x0f)); // Darker Green
                    break;
                case TweakStatus.Default:
                    StatusText = "Não Otimizado";
                    StatusIcon = SymbolRegular.DismissCircle24;
                    StatusColor = new SolidColorBrush(Color.FromRgb(0xc4, 0x2b, 0x1c)); // Red
                    break;
                case TweakStatus.Modified:
                    StatusText = "Modificado";
                    StatusIcon = SymbolRegular.Edit24;
                    StatusColor = new SolidColorBrush(Color.FromRgb(202, 80, 16)); // Orange
                    break;
                default:
                    StatusText = "Desconhecido";
                    StatusIcon = SymbolRegular.QuestionCircle24;
                    StatusColor = Brushes.Gray;
                    break;
            }

            // Freeze brushes for performance if not already frozen (though we create new ones)
            if (StatusColor.CanFreeze) StatusColor.Freeze();
        }

        /// <summary>
        /// Refreshes the status of the tweak asynchronously.
        /// </summary>
        public async Task RefreshStatusAsync()
        {
             await Task.Run(() => _tweak.CheckStatus());
        }
    }
}
