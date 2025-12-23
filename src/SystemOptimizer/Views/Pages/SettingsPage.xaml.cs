using Wpf.Ui.Controls;
using SystemOptimizer.ViewModels;

namespace SystemOptimizer.Views.Pages;

public partial class SettingsPage : FluentPage
{
    public SettingsViewModel ViewModel { get; }

    // O ViewModel será injetado automaticamente
    public SettingsPage(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this; // Permite ligar propriedades UI diretas se necessário
        
        InitializeComponent();
        
        // Define o DataContext do XAML para o ViewModel
        DataContext = ViewModel;
    }
}
