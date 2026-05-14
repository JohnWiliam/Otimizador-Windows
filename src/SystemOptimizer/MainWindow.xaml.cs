using System;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Extensions.DependencyInjection;
using SystemOptimizer.ViewModels;
using SystemOptimizer.Views.Pages;
using Windows.Graphics;

namespace SystemOptimizer;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    public MainWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        if (Content is FrameworkElement root) root.DataContext = ViewModel;
        RootNavigation.DataContext = ViewModel;

        Title = ViewModel.ApplicationTitle;
        AppWindow.Resize(new SizeInt32(1120, 760));
        TryUseMicaBackdrop();
        Navigate(typeof(PrivacyPage));
    }

    public XamlRoot? DialogXamlRoot => Content?.XamlRoot;

    public void Navigate(Type pageType)
    {
        if (ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }
    }

    private void RootNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag)
            return;

        var pageType = tag switch
        {
            "Privacy" => typeof(PrivacyPage),
            "Performance" => typeof(PerformancePage),
            "Network" => typeof(NetworkPage),
            "Security" => typeof(SecurityPage),
            "Search" => typeof(SearchPage),
            "Appearance" => typeof(AppearancePage),
            "Tweaks" => typeof(TweaksPage),
            "Cleanup" => typeof(CleanupPage),
            "Settings" => typeof(SettingsPage),
            _ => typeof(PrivacyPage)
        };

        Navigate(pageType);
    }

    private void TryUseMicaBackdrop()
    {
        if (MicaController.IsSupported())
        {
            SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };
        }
    }
}
