using System.Windows.Controls;
using SystemOptimizer.ViewModels;

namespace SystemOptimizer.Views.Pages;

public partial class NetworkPage : Page
{
    public NetworkPage(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
