using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using SystemOptimizer.Properties;
using SystemOptimizer.ViewModels;
using SystemOptimizer.Views.Controls;

namespace SystemOptimizer.Views.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Content = BuildContent();
    }

    private UIElement BuildContent()
    {
        var language = new ComboBox { Header = Resources.Settings_Language, ItemsSource = ViewModel.Languages, MinWidth = 240 };
        language.SetBinding(Selector.SelectedItemProperty, new Binding { Source = ViewModel, Path = new PropertyPath(nameof(ViewModel.CurrentLanguage)), Mode = BindingMode.TwoWay });

        var theme = new ComboBox { Header = Resources.Settings_Appearance, ItemsSource = ViewModel.ThemeOptions, DisplayMemberPath = "Name", MinWidth = 240 };
        theme.SetBinding(Selector.SelectedItemProperty, new Binding { Source = ViewModel, Path = new PropertyPath(nameof(ViewModel.CurrentThemeOption)), Mode = BindingMode.TwoWay });

        var persistence = new ToggleSwitch { Header = Resources.Settings_Persistence, OnContent = Resources.Status_Optimized, OffContent = Resources.Status_Default };
        persistence.SetBinding(ToggleSwitch.IsOnProperty, new Binding { Source = ViewModel, Path = new PropertyPath(nameof(ViewModel.IsPersistenceEnabled)), Mode = BindingMode.TwoWay });

        var keepInstalled = new ToggleSwitch { Header = Resources.Settings_KeepInstalled, OnContent = Resources.Status_Optimized, OffContent = Resources.Status_Default };
        keepInstalled.SetBinding(ToggleSwitch.IsOnProperty, new Binding { Source = ViewModel, Path = new PropertyPath(nameof(ViewModel.IsKeepInstalledEnabled)), Mode = BindingMode.TwoWay });

        var updates = new Button { Content = Resources.Btn_CheckUpdate, Style = Application.Current.Resources["AccentButtonStyle"] as Style };
        updates.Click += (_, _) =>
        {
            if (ViewModel.CheckForUpdatesCommand.CanExecute(null)) ViewModel.CheckForUpdatesCommand.Execute(null);
        };

        return FluentPageFactory.Wrap(
            Resources.Settings_Title,
            FluentPageFactory.Card(language, theme),
            FluentPageFactory.Card(persistence, keepInstalled),
            FluentPageFactory.Card(
                new TextBlock { Text = Resources.Settings_UpdateCheckDesc, TextWrapping = TextWrapping.Wrap, Opacity = 0.8 },
                updates));
    }
}
