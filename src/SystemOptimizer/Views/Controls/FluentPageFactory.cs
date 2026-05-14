using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using SystemOptimizer.ViewModels;

namespace SystemOptimizer.Views.Controls;

internal static class FluentPageFactory
{
    public static ScrollViewer CreateTweakPage(string title, string category, ObservableCollection<TweakViewModel> items, MainViewModel viewModel)
    {
        var root = new StackPanel { Margin = new Thickness(32), Spacing = 18 };
        root.Children.Add(new TextBlock { Text = title, Style = Application.Current.Resources["TitleTextBlockStyle"] as Style });

        var commandBar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var apply = new Button { Content = "Aplicar selecionados", Style = Application.Current.Resources["AccentButtonStyle"] as Style };
        apply.Click += (_, _) => Execute(viewModel.ApplySelectedCommand, category);
        var revert = new Button { Content = "Restaurar selecionados" };
        revert.Click += (_, _) => Execute(viewModel.RevertSelectedCommand, category);
        commandBar.Children.Add(apply);
        commandBar.Children.Add(revert);
        root.Children.Add(commandBar);

        foreach (var item in items)
        {
            root.Children.Add(CreateTweakCard(item, viewModel));
        }

        if (items.Count == 0)
        {
            root.Children.Add(new InfoBar
            {
                IsOpen = true,
                Severity = InfoBarSeverity.Informational,
                Title = "Nenhuma opção carregada",
                Message = "As opções aparecerão aqui após a inicialização do serviço de tweaks."
            });
        }

        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    public static Border CreateTweakCard(TweakViewModel item, MainViewModel viewModel)
    {
        var card = new Border
        {
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(18),
            Background = Application.Current.Resources["CardBackgroundFillColorDefaultBrush"] as Brush,
            BorderBrush = Application.Current.Resources["CardStrokeColorDefaultBrush"] as Brush,
            BorderThickness = new Thickness(1)
        };

        var grid = new Grid { ColumnSpacing = 14 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var check = new CheckBox { VerticalAlignment = VerticalAlignment.Center };
        check.SetBinding(ToggleButton.IsCheckedProperty, new Binding { Source = item, Path = new PropertyPath(nameof(item.IsSelected)), Mode = BindingMode.TwoWay });
        Grid.SetColumn(check, 0);
        grid.Children.Add(check);

        var text = new StackPanel { Spacing = 6 };
        text.Children.Add(new TextBlock { Text = item.Title, Style = Application.Current.Resources["BodyStrongTextBlockStyle"] as Style, TextWrapping = TextWrapping.Wrap });
        text.Children.Add(new TextBlock { Text = item.Description, Opacity = 0.78, TextWrapping = TextWrapping.Wrap });
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap };
        status.SetBinding(TextBlock.TextProperty, new Binding { Source = item, Path = new PropertyPath(nameof(item.StatusDisplay)), Mode = BindingMode.OneWay });
        status.SetBinding(TextBlock.ForegroundProperty, new Binding { Source = item, Path = new PropertyPath(nameof(item.StatusColor)), Mode = BindingMode.OneWay });
        text.Children.Add(status);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        var apply = new Button { Content = "Aplicar", Style = Application.Current.Resources["AccentButtonStyle"] as Style };
        apply.Click += (_, _) => Execute(viewModel.ApplySingleCommand, item.Id);
        var revert = new Button { Content = "Restaurar" };
        revert.Click += (_, _) => Execute(viewModel.RevertSingleCommand, item.Id);
        actions.Children.Add(apply);
        actions.Children.Add(revert);
        Grid.SetColumn(actions, 2);
        grid.Children.Add(actions);

        card.Child = grid;
        return card;
    }

    public static ScrollViewer Wrap(string title, params UIElement[] elements)
    {
        var root = new StackPanel { Margin = new Thickness(32), Spacing = 18 };
        root.Children.Add(new TextBlock { Text = title, Style = Application.Current.Resources["TitleTextBlockStyle"] as Style });
        foreach (var element in elements) root.Children.Add(element);
        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    public static Border Card(params UIElement[] elements)
    {
        var panel = new StackPanel { Spacing = 12 };
        foreach (var element in elements) panel.Children.Add(element);
        return new Border
        {
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(18),
            Background = Application.Current.Resources["CardBackgroundFillColorDefaultBrush"] as Brush,
            BorderBrush = Application.Current.Resources["CardStrokeColorDefaultBrush"] as Brush,
            BorderThickness = new Thickness(1),
            Child = panel
        };
    }

    private static void Execute(System.Windows.Input.ICommand command, object? parameter = null)
    {
        if (command.CanExecute(parameter)) command.Execute(parameter);
    }
}
