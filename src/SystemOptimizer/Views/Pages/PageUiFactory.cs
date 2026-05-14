using System;
using System.Collections.ObjectModel;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SystemOptimizer.Properties;
using SystemOptimizer.ViewModels;

namespace SystemOptimizer.Views.Pages;

internal static class PageUiFactory
{
    public static void BuildTweakPage(StackPanel host, string title, string subtitle, string category, ObservableCollection<TweakViewModel> tweaks, MainViewModel viewModel, bool includeExplorerRestart = false)
    {
        host.Children.Clear();
        host.Children.Add(Header(title, subtitle));

        var commandBar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        commandBar.Children.Add(new Button { Content = Resources.Btn_Apply, Command = viewModel.ApplySelectedCommand, CommandParameter = category });
        commandBar.Children.Add(new Button { Content = Resources.Btn_Restore, Command = viewModel.RevertSelectedCommand, CommandParameter = category });
        if (includeExplorerRestart)
        {
            commandBar.Children.Add(new Button { Content = Resources.Btn_RestartExplorer, Command = viewModel.RestartExplorerCommand });
        }
        host.Children.Add(commandBar);

        foreach (var tweak in tweaks)
        {
            host.Children.Add(TweakCard(tweak));
        }
    }

    public static FrameworkElement Header(string title, string subtitle)
    {
        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(new TextBlock { Text = title, Style = (Style)Application.Current.Resources["TitleTextBlockStyle"] });
        panel.Children.Add(new TextBlock { Text = subtitle, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Colors.Gray) });
        return panel;
    }

    public static FrameworkElement TweakCard(TweakViewModel tweak)
    {
        var border = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(12),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1)
        };

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var checkBox = new CheckBox { IsChecked = tweak.IsSelected, VerticalAlignment = VerticalAlignment.Center };
        checkBox.Checked += (_, _) => tweak.IsSelected = true;
        checkBox.Unchecked += (_, _) => tweak.IsSelected = false;
        Grid.SetColumn(checkBox, 0);
        grid.Children.Add(checkBox);

        var textPanel = new StackPanel { Spacing = 4 };
        textPanel.Children.Add(new TextBlock { Text = tweak.Title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        textPanel.Children.Add(new TextBlock { Text = tweak.Description, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Colors.Gray) });
        Grid.SetColumn(textPanel, 1);
        grid.Children.Add(textPanel);

        var status = new TextBlock
        {
            Text = tweak.StatusText,
            Foreground = tweak.StatusColor,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(status, 2);
        grid.Children.Add(status);

        border.Child = grid;
        return border;
    }
}
