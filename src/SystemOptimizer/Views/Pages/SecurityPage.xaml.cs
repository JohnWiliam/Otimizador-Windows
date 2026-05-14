using Microsoft.UI.Xaml.Controls;
using SystemOptimizer.Properties;
using SystemOptimizer.ViewModels;
using SystemOptimizer.Views.Controls;

namespace SystemOptimizer.Views.Pages;

public sealed partial class SecurityPage : Page
{
    public SecurityPage(MainViewModel viewModel)
    {
        InitializeComponent();
        Content = FluentPageFactory.CreateTweakPage(Resources.Nav_Security, "Security", viewModel.SecurityTweaks, viewModel);
    }
}
