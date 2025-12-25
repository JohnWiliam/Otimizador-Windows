using SystemOptimizer.ViewModels;
using Wpf.Ui.Abstractions;
using System.Windows.Controls;

namespace SystemOptimizer.Views.Pages;

public partial class SearchFixPage : Page, INavigableView<SearchFixViewModel>
{
    public SearchFixViewModel ViewModel { get; }

    public SearchFixPage(SearchFixViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;
        InitializeComponent();
    }
}
