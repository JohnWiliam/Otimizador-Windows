using System.Windows.Controls;
using SystemOptimizer.ViewModels;

namespace SystemOptimizer.Views.Pages;

public partial class SecurityPage : Page
{
    public SecurityPage(MainViewModel viewModel)
    {
        InitializeComponent();

        // Define o DataContext para o ViewModel injetado.
        // Garante consistência com as outras páginas e funcionamento dos comandos.
        DataContext = viewModel;
    }
}
