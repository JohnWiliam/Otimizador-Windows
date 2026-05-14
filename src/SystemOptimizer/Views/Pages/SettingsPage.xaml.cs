using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SystemOptimizer.Properties;
using SystemOptimizer.ViewModels;

namespace SystemOptimizer.Views.Pages;

public sealed class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        ViewModel = App.Services.GetRequiredService<SettingsViewModel>();
        DataContext = ViewModel;
        Content = BuildContent();
    }

    private UIElement BuildContent()
    {
        var root = new ScrollViewer { Padding = new Thickness(28) };
        var stack = new StackPanel { Spacing = 18 };
        root.Content = stack;
        stack.Children.Add(new TextBlock { Text = Resources.Settings_Title, FontSize = 28, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });

        var language = new ComboBox { Header = Resources.Settings_Language, ItemsSource = ViewModel.Languages, SelectedItem = ViewModel.CurrentLanguage, MinWidth = 240 };
        language.SelectionChanged += (_, _) => { if (language.SelectedItem is string value) ViewModel.CurrentLanguage = value; };
        stack.Children.Add(language);

        var theme = new ComboBox { Header = Resources.Settings_ThemeDesc, ItemsSource = ViewModel.ThemeOptions, SelectedItem = ViewModel.CurrentThemeOption, DisplayMemberPath = "Name", MinWidth = 240 };
        theme.SelectionChanged += (_, _) => { if (theme.SelectedItem is ThemeOption option) ViewModel.CurrentThemeOption = option; };
        stack.Children.Add(theme);

        var persistence = new ToggleSwitch { Header = Resources.Settings_Persistence, IsOn = ViewModel.IsPersistenceEnabled };
        persistence.Toggled += (_, _) => ViewModel.IsPersistenceEnabled = persistence.IsOn;
        stack.Children.Add(persistence);

        var keepInstalled = new ToggleSwitch { Header = Resources.Settings_KeepInstalled, IsOn = ViewModel.IsKeepInstalledEnabled };
        keepInstalled.Toggled += (_, _) => ViewModel.IsKeepInstalledEnabled = keepInstalled.IsOn;
        stack.Children.Add(keepInstalled);

        var update = new Button { Content = Resources.Btn_CheckUpdate, Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
        update.Click += (_, _) => ViewModel.CheckForUpdatesCommand.Execute(null);
        stack.Children.Add(update);
        return root;
    }
}
