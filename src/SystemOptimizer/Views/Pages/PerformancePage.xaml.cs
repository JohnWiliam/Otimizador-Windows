using System.Windows.Controls;
using SystemOptimizer.ViewModels;

namespace SystemOptimizer.Views.Pages;

public partial class PerformancePage : Page
{
    public PerformancePage(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
