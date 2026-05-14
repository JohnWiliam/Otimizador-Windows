using Microsoft.UI.Xaml.Controls;
using SystemOptimizer.Properties;
using SystemOptimizer.ViewModels;
using SystemOptimizer.Views.Controls;

namespace SystemOptimizer.Views.Pages;

public sealed partial class PrivacyPage : Page
{
    public PrivacyPage(MainViewModel viewModel)
    {
        InitializeComponent();
        Content = FluentPageFactory.CreateTweakPage(Resources.Nav_Privacy, "Privacy", viewModel.PrivacyTweaks, viewModel);
    }
}
