using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;
using Wpf.Ui.Appearance;

namespace SystemOptimizer.ViewModels;

// Classe auxiliar para as opções do ComboBox
public record ThemeOption(string Name, ApplicationTheme Theme);

public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private string _currentLanguage = "Português";

    public ObservableCollection<string> Languages { get; } = ["Português"];

    // Lista de opções de tema para o ComboBox
    public ObservableCollection<ThemeOption> ThemeOptions { get; } = 
    [
        new("Padrão do Sistema", ApplicationTheme.Unknown),
        new("Claro", ApplicationTheme.Light),
        new("Escuro", ApplicationTheme.Dark)
    ];

    [ObservableProperty]
    private ThemeOption _currentThemeOption;

    public SettingsViewModel()
    {
        // Define "Padrão do Sistema" como a opção inicial padrão
        // (Ou podes lógica para detetar qual o tema atual e selecionar a opção correta)
        _currentThemeOption = ThemeOptions.First(x => x.Theme == ApplicationTheme.Unknown);
        
        // Aplica o tema inicial
        UpdateTheme(_currentThemeOption.Theme);
    }

    // Este método é chamado automaticamente sempre que o usuário muda a seleção no ComboBox
    partial void OnCurrentThemeOptionChanged(ThemeOption value)
    {
        if (value != null)
        {
            UpdateTheme(value.Theme);
        }
    }

    private void UpdateTheme(ApplicationTheme theme)
    {
        if (theme == ApplicationTheme.Unknown)
        {
            // CORREÇÃO: Usamos o método próprio ApplySystemTheme() em vez de converter manualmente
            ApplicationThemeManager.ApplySystemTheme();
        }
        else
        {
            // Aplica o tema Claro ou Escuro selecionado
            ApplicationThemeManager.Apply(theme);
        }
    }
}
