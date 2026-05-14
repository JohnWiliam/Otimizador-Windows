using Microsoft.UI.Xaml.Controls;
using SystemOptimizer.Properties;
using SystemOptimizer.ViewModels;
using SystemOptimizer.Views.Controls;

namespace SystemOptimizer.Views.Pages;

public sealed partial class AppearancePage : Page
{
    public AppearancePage(MainViewModel viewModel)
    {
        InitializeComponent();
        Content = FluentPageFactory.CreateTweakPage(Resources.Nav_Visual, "Appearance", viewModel.AppearanceTweaks, viewModel);
    }
}
