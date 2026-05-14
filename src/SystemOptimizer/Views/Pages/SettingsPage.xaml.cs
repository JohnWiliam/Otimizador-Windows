using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SystemOptimizer.Properties;
using SystemOptimizer.ViewModels;

namespace SystemOptimizer.Views.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        var viewModel = App.GetService<SettingsViewModel>();
        ContentHost.Children.Add(PageUiFactory.Header(Resources.Settings_Title, Resources.Settings_General));

        var languageBox = new ComboBox { Header = Resources.Settings_Language, Width = 260, ItemsSource = viewModel.Languages, SelectedItem = viewModel.CurrentLanguage };
        languageBox.SelectionChanged += (_, _) => viewModel.CurrentLanguage = languageBox.SelectedItem?.ToString() ?? viewModel.CurrentLanguage;
        ContentHost.Children.Add(languageBox);

        var themeBox = new ComboBox { Header = Resources.Settings_Appearance, Width = 260, ItemsSource = viewModel.ThemeOptions, SelectedItem = viewModel.CurrentThemeOption, DisplayMemberPath = "Name" };
        themeBox.SelectionChanged += (_, _) =>
        {
            if (themeBox.SelectedItem is ThemeOption option) viewModel.CurrentThemeOption = option;
        };
        ContentHost.Children.Add(themeBox);

        var persistence = new ToggleSwitch { Header = Resources.Settings_Persistence, OnContent = "Ativado", OffContent = "Desativado", IsOn = viewModel.IsPersistenceEnabled };
        persistence.Toggled += (_, _) => viewModel.IsPersistenceEnabled = persistence.IsOn;
        ContentHost.Children.Add(persistence);

        var keepInstalled = new ToggleSwitch { Header = Resources.Settings_KeepInstalled, OnContent = "Ativado", OffContent = "Desativado", IsOn = viewModel.IsKeepInstalledEnabled };
        keepInstalled.Toggled += (_, _) => viewModel.IsKeepInstalledEnabled = keepInstalled.IsOn;
        ContentHost.Children.Add(keepInstalled);

        ContentHost.Children.Add(new Button { Content = Resources.Btn_CheckUpdate, Command = viewModel.CheckForUpdatesCommand });
    }
}
