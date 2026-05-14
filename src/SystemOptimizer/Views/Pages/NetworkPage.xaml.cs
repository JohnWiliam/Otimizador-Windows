using Microsoft.UI.Xaml.Controls;
using SystemOptimizer.ViewModels;

namespace SystemOptimizer.Views.Pages;

public sealed partial class NetworkPage : Page
{
    public NetworkPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetService(typeof(MainViewModel));
    }
}
