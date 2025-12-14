using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;
using SystemOptimizer.Models;

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

        public ITweak Tweak => _tweak;

        public TweakViewModel(ITweak tweak)
        {
            _tweak = tweak;
            UpdateStatusUI();
        }

        public void UpdateStatusUI()
        {
            // _tweak.CheckStatus() is now called during Service Initialization, 
            // but we can call it again if needed.
            // However, calling it here on UI thread might be slow if individual check is slow.
            // For now, assume Status is already populated by Service init.
            
            switch (_tweak.Status)
            {
                case TweakStatus.Optimized:
                    StatusText = "● OTIMIZADO";
                    StatusColor = new SolidColorBrush(Color.FromRgb(16, 124, 16)); // #107C10
                    break;
                case TweakStatus.Default:
                    StatusText = "● PADRÃO";
                    StatusColor = new SolidColorBrush(Color.FromRgb(0, 120, 212)); // #0078D4
                    break;
                case TweakStatus.Modified:
                    StatusText = "● MODIFICADO";
                    StatusColor = new SolidColorBrush(Color.FromRgb(202, 80, 16)); // #CA5010
                    break;
                default:
                    StatusText = "○ INDEFINIDO";
                    StatusColor = Brushes.Gray;
                    break;
            }
        }
    }
}
