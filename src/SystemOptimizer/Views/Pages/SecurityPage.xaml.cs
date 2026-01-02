using SystemOptimizer.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace SystemOptimizer.Views.Pages;

public partial class SecurityPage : INavigableView<MainViewModel>
{
    public MainViewModel ViewModel { get; }

    public SecurityPage(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
