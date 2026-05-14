using Microsoft.UI.Xaml.Controls;
using SystemOptimizer.ViewModels;

namespace SystemOptimizer.Views.Pages;

public sealed partial class PerformancePage : Page
{
    public PerformancePage()
    {
        InitializeComponent();
        DataContext = App.Services.GetService(typeof(MainViewModel));
    }
}
