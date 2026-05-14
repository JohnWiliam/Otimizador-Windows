using Microsoft.UI.Xaml.Controls;
using SystemOptimizer.ViewModels;

namespace SystemOptimizer.Views.Pages;

public sealed partial class TweaksPage : Page
{
    public TweaksPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetService(typeof(MainViewModel));
    }
}
