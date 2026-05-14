using Microsoft.UI.Xaml.Controls;
using SystemOptimizer.Properties;
using SystemOptimizer.ViewModels;

namespace SystemOptimizer.Views.Pages;

public sealed partial class PerformancePage : Page
{
    public PerformancePage()
    {
        InitializeComponent();
        var viewModel = App.GetService<MainViewModel>();
        PageUiFactory.BuildTweakPage(ContentHost, Resources.Nav_Performance, "Aprimore resposta do sistema com opções seguras e reversíveis.", "Performance", viewModel.PerformanceTweaks, viewModel, false);
    }
}
