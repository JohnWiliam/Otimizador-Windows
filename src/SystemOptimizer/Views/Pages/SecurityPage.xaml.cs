using Microsoft.UI.Xaml.Controls;
using SystemOptimizer.Properties;
using SystemOptimizer.ViewModels;

namespace SystemOptimizer.Views.Pages;

public sealed partial class SecurityPage : Page
{
    public SecurityPage()
    {
        InitializeComponent();
        var viewModel = App.GetService<MainViewModel>();
        PageUiFactory.BuildTweakPage(ContentHost, Resources.Nav_Security, "Fortaleça recursos de proteção mantendo visibilidade das mudanças.", "Security", viewModel.SecurityTweaks, viewModel, false);
    }
}
