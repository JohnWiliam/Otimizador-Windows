using System.Windows.Controls; // Necessário para a classe Page padrão
using Wpf.Ui.Controls;
using SystemOptimizer.ViewModels;

namespace SystemOptimizer.Views.Pages;

public partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        
        InitializeComponent();
        
        // Define o DataContext para o ViewModel injetado
        DataContext = ViewModel;
    }
}
