using Microsoft.UI.Xaml.Controls;
using SystemOptimizer.Properties;
using SystemOptimizer.ViewModels;
using SystemOptimizer.Views.Controls;

namespace SystemOptimizer.Views.Pages;

public sealed partial class SearchPage : Page
{
    public SearchPage(MainViewModel viewModel)
    {
        InitializeComponent();
        Content = FluentPageFactory.CreateTweakPage(Resources.Nav_Search, "Search", viewModel.SearchTweaks, viewModel);
    }
}
