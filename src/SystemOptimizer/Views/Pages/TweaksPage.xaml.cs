using System.Windows.Controls;
using SystemOptimizer.ViewModels;

namespace SystemOptimizer.Views.Pages
{
    public partial class TweaksPage : Page
    {
        public TweaksPage(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
