using Microsoft.UI.Xaml.Controls;
using SystemOptimizer.ViewModels;

namespace SystemOptimizer.Views.Pages;

public sealed partial class SearchPage : Page
{
    public SearchPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetService(typeof(MainViewModel));
    }
}
