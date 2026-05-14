using Microsoft.UI.Xaml.Controls;
using SystemOptimizer.ViewModels;

namespace SystemOptimizer.Views.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetService(typeof(SettingsViewModel));
    }
}
