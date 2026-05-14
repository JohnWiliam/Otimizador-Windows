using Microsoft.UI.Xaml.Controls;
using SystemOptimizer.Properties;
using SystemOptimizer.ViewModels;

namespace SystemOptimizer.Views.Pages;

public sealed partial class PrivacyPage : Page
{
    public PrivacyPage()
    {
        InitializeComponent();
        var viewModel = App.GetService<MainViewModel>();
        PageUiFactory.BuildTweakPage(ContentHost, Resources.Nav_Privacy, "Controle telemetria, sugestões e permissões com ajustes nativos do Windows.", "Privacy", viewModel.PrivacyTweaks, viewModel, false);
    }
}
