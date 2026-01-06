using System.Windows.Controls;
using SystemOptimizer.ViewModels;

namespace SystemOptimizer.Views.Pages;

public partial class SearchPage : Page
{
    public SearchPage(MainViewModel viewModel)
    {
        InitializeComponent();
        
        // Define o DataContext para o ViewModel injetado.
        // Isso permite que os Bindings do XAML (ex: {Binding SearchTweaks}) funcionem corretamente.
        DataContext = viewModel;
    }
}
