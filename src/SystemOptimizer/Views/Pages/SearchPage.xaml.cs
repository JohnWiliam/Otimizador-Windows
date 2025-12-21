using System.Windows.Controls;
using SystemOptimizer.ViewModels;

namespace SystemOptimizer.Views.Pages
{
    public partial class SearchPage : Page
    {
        public SearchPage(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
