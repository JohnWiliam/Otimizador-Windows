using Microsoft.UI.Xaml.Controls;
using SystemOptimizer.ViewModels;

namespace SystemOptimizer.Views.Pages;

public sealed partial class SecurityPage : Page
{
    public SecurityPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetService(typeof(MainViewModel));
    }
}
