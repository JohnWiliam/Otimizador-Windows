using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using SystemOptimizer.Properties;
using SystemOptimizer.ViewModels;
using SystemOptimizer.Views.Controls;

namespace SystemOptimizer.Views.Pages;

public sealed partial class CleanupPage : Page
{
    private readonly MainViewModel _viewModel;

    public CleanupPage(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        Content = BuildContent();
    }

    private UIElement BuildContent()
    {
        var runButton = new Button { Content = Resources.Btn_RunCleanup, Style = Application.Current.Resources["AccentButtonStyle"] as Style };
        runButton.Click += (_, _) =>
        {
            if (_viewModel.RunCleanupCommand.CanExecute(null)) _viewModel.RunCleanupCommand.Execute(null);
        };

        var progress = new ProgressBar { Minimum = 0, Maximum = 100, Height = 6 };
        progress.SetBinding(ProgressBar.ValueProperty, new Binding { Source = _viewModel, Path = new PropertyPath(nameof(_viewModel.CleanupProgressPercentage)), Mode = BindingMode.OneWay });

        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.8 };
        status.SetBinding(TextBlock.TextProperty, new Binding { Source = _viewModel, Path = new PropertyPath(nameof(_viewModel.CleanupProgressCategory)), Mode = BindingMode.OneWay });

        var logs = new ListView
        {
            ItemsSource = _viewModel.CleanupLogs,
            MinHeight = 260,
            SelectionMode = ListViewSelectionMode.None
        };
        logs.ItemTemplate = CreateLogTemplate();

        return FluentPageFactory.Wrap(
            Resources.Cleanup_Title,
            FluentPageFactory.Card(
                new TextBlock { Text = Resources.Cleanup_CacheDesc, TextWrapping = TextWrapping.Wrap, Opacity = 0.82 },
                runButton,
                progress,
                status),
            FluentPageFactory.Card(
                new TextBlock { Text = Resources.Cleanup_LogTitle, Style = Application.Current.Resources["SubtitleTextBlockStyle"] as Style },
                logs));
    }

    private static DataTemplate CreateLogTemplate()
    {
        return new DataTemplate(() =>
        {
            var text = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 4) };
            text.SetBinding(TextBlock.TextProperty, new Binding { Path = new PropertyPath("Message") });
            return text;
        });
    }
}
