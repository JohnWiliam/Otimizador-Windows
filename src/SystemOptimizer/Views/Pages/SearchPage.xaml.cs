using SystemOptimizer.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace SystemOptimizer.Views.Pages;

public partial class SearchPage : INavigableView<MainViewModel>
{
    public MainViewModel ViewModel { get; }

    public SearchPage(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
