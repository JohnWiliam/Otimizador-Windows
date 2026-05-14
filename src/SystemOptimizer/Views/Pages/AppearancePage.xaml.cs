using Microsoft.UI.Xaml.Controls;
using SystemOptimizer.Properties;
using SystemOptimizer.ViewModels;

namespace SystemOptimizer.Views.Pages;

public sealed partial class AppearancePage : Page
{
    public AppearancePage()
    {
        InitializeComponent();
        var viewModel = App.GetService<MainViewModel>();
        PageUiFactory.BuildTweakPage(ContentHost, Resources.Nav_Visual, "Personalize a experiência visual com Fluent 2, Mica e preferências nativas.", "Appearance", viewModel.AppearanceTweaks, viewModel, false);
    }
}
