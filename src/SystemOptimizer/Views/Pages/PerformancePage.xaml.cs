using Microsoft.UI.Xaml.Controls;
using SystemOptimizer.Properties;
using SystemOptimizer.ViewModels;
using SystemOptimizer.Views.Controls;

namespace SystemOptimizer.Views.Pages;

public sealed partial class PerformancePage : Page
{
    public PerformancePage(MainViewModel viewModel)
    {
        InitializeComponent();
        Content = FluentPageFactory.CreateTweakPage(Resources.Nav_Performance, "Performance", viewModel.PerformanceTweaks, viewModel);
    }
}
