using System.Windows.Controls;
using Wpf.Ui.Controls;
using SystemOptimizer.ViewModels;

namespace SystemOptimizer.Views.Pages;

public partial class SearchFixPage : Page
{
    public SearchFixViewModel ViewModel { get; }

    public SearchFixPage(SearchFixViewModel viewModel)
    {
        ViewModel = viewModel;
        
        InitializeComponent();
        
        // Configura o DataContext diretamente para o ViewModel, 
        // seguindo o padrão da SettingsPage e PrivacyPage
        DataContext = ViewModel;
    }
}
