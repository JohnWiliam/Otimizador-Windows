using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SystemOptimizer.Properties;
using SystemOptimizer.ViewModels;

namespace SystemOptimizer.Views.Pages;

public abstract class TweakCategoryPage : Page
{
    protected MainViewModel ViewModel { get; }
    private readonly StackPanel _itemsPanel = new() { Spacing = 10 };
    private readonly List<Action> _subscriptions = [];

    protected TweakCategoryPage(string title, string category, ObservableCollection<TweakViewModel> tweaks)
    {
        ViewModel = App.Services.GetRequiredService<MainViewModel>();
        DataContext = ViewModel;

        var applyAll = new Button { Content = Resources.Btn_Apply, Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
        applyAll.Click += (_, _) => ViewModel.ApplySelectedCommand.Execute(category);

        var revertAll = new Button { Content = Resources.Btn_Restore };
        revertAll.Click += (_, _) => ViewModel.RevertSelectedCommand.Execute(category);

        var header = new Grid { Margin = new Thickness(28, 24, 28, 12), ColumnSpacing = 12 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock { Text = title, FontSize = 28, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        Grid.SetColumn(applyAll, 1);
        Grid.SetColumn(revertAll, 2);
        header.Children.Add(applyAll);
        header.Children.Add(revertAll);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(header);

        var scroll = new ScrollViewer { Padding = new Thickness(28, 0, 28, 28), Content = _itemsPanel };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);
        Content = root;

        Loaded += (_, _) => RenderItems(tweaks);
        Unloaded += (_, _) => ClearRenderedItems();
        tweaks.CollectionChanged += (_, _) => RenderItems(tweaks);
    }

    private void RenderItems(ObservableCollection<TweakViewModel> tweaks)
    {
        ClearRenderedItems();
        foreach (var tweak in tweaks)
        {
            _itemsPanel.Children.Add(CreateTweakCard(tweak));
        }
    }

    private void ClearRenderedItems()
    {
        foreach (var unsubscribe in _subscriptions)
        {
            unsubscribe();
        }

        _subscriptions.Clear();
        _itemsPanel.Children.Clear();
    }

    private UIElement CreateTweakCard(TweakViewModel tweak)
    {
        var select = new CheckBox { IsChecked = tweak.IsSelected, VerticalAlignment = VerticalAlignment.Center };
        select.Checked += (_, _) => tweak.IsSelected = true;
        select.Unchecked += (_, _) => tweak.IsSelected = false;

        var title = new TextBlock { Text = tweak.Title, FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.WrapWholeWords };
        var description = new TextBlock { Text = tweak.Description, Opacity = 0.78, TextWrapping = TextWrapping.WrapWholeWords, Margin = new Thickness(0, 4, 0, 0) };
        var status = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 10, 0, 0) };
        var symbol = new SymbolIcon { Symbol = tweak.StatusIcon, Foreground = tweak.StatusColor };
        var statusText = new TextBlock { Text = tweak.StatusText, Foreground = tweak.StatusColor, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        status.Children.Add(symbol);
        status.Children.Add(statusText);

        PropertyChangedEventHandler handler = (_, e) =>
        {
            if (e.PropertyName is nameof(TweakViewModel.StatusText) or nameof(TweakViewModel.StatusIcon) or nameof(TweakViewModel.StatusColor))
            {
                App.DispatchToUi(() =>
                {
                    symbol.Symbol = tweak.StatusIcon;
                    symbol.Foreground = tweak.StatusColor;
                    statusText.Text = tweak.StatusText;
                    statusText.Foreground = tweak.StatusColor;
                });
            }
        };
        tweak.PropertyChanged += handler;
        _subscriptions.Add(() => tweak.PropertyChanged -= handler);

        var text = new StackPanel();
        text.Children.Add(title);
        text.Children.Add(description);
        text.Children.Add(status);

        var apply = new Button { Content = Resources.Btn_Apply, Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
        apply.Click += (_, _) => ViewModel.ApplySingleCommand.Execute(tweak.Id);
        var restore = new Button { Content = Resources.Btn_Restore };
        restore.Click += (_, _) => ViewModel.RevertSingleCommand.Execute(tweak.Id);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        buttons.Children.Add(apply);
        buttons.Children.Add(restore);

        var grid = new Grid { ColumnSpacing = 16 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(select, 0);
        Grid.SetColumn(text, 1);
        Grid.SetColumn(buttons, 2);
        grid.Children.Add(select);
        grid.Children.Add(text);
        grid.Children.Add(buttons);

        return new Border
        {
            Padding = new Thickness(18),
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(48, 128, 128, 128)),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            Child = grid
        };
    }
}
