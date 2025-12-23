using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace SystemOptimizer.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private ApplicationTheme _currentTheme = ApplicationTheme.Unknown;

    [ObservableProperty]
    private string _currentLanguage = "Português";

    public ObservableCollection<string> Languages { get; } = ["Português"];

    public SettingsViewModel()
    {
        // Define o tema atual baseado no estado do sistema ao iniciar
        CurrentTheme = ApplicationThemeManager.GetAppTheme();
    }

    [RelayCommand]
    private void ChangeTheme(ApplicationTheme theme)
    {
        if (CurrentTheme == theme)
            return;

        ApplicationThemeManager.Apply(theme);
        CurrentTheme = theme;
    }
    
    // Futuramente, aqui poderás adicionar lógica para mudar o idioma
}
