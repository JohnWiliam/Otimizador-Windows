using Microsoft.UI.Xaml.Controls;
using SystemOptimizer.Properties;
using SystemOptimizer.ViewModels;

namespace SystemOptimizer.Views.Pages;

public sealed partial class NetworkPage : Page
{
    public NetworkPage()
    {
        InitializeComponent();
        var viewModel = App.GetService<MainViewModel>();
        PageUiFactory.BuildTweakPage(ContentHost, Resources.Nav_Network, "Ajustes modernos para pilha de rede, DNS e conectividade.", "Network", viewModel.NetworkTweaks, viewModel, false);
    }
}
