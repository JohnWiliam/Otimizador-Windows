using Microsoft.UI.Xaml.Controls;
using SystemOptimizer.Properties;
using SystemOptimizer.ViewModels;

namespace SystemOptimizer.Views.Pages;

public sealed partial class TweaksPage : Page
{
    public TweaksPage()
    {
        InitializeComponent();
        var viewModel = App.GetService<MainViewModel>();
        PageUiFactory.BuildTweakPage(ContentHost, Resources.Nav_Tweaks, "Ferramentas avançadas organizadas em cartões modernos e confiáveis.", "Tweaks", viewModel.TweaksPageItems, viewModel, false);
    }
}
