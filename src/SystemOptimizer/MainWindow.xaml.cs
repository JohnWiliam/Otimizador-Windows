using System;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SystemOptimizer.Helpers;
using SystemOptimizer.Services;
using SystemOptimizer.ViewModels;
using SystemOptimizer.Views.Pages;

namespace SystemOptimizer;

public sealed partial class MainWindow : Window
{
    private readonly StartupActivationState _activationState;

    public MainViewModel ViewModel { get; }

    public MainWindow(MainViewModel viewModel, StartupActivationState activationState)
    {
        ViewModel = viewModel;
        _activationState = activationState;
        InitializeComponent();
        RootGrid.DataContext = ViewModel;
        ConfigureChrome();
    }

    public void NavigateTo(Type pageType) => ContentFrame.Navigate(pageType);

    private async void RootNavigation_Loaded(object sender, RoutedEventArgs e)
    {
        Logger.Log("MainWindow carregada; inicializando dados nativos WinUI.");
        await ViewModel.InitializeAsync();

        if (_activationState.OpenSettingsRequested)
        {
            NavigateByTag("settings");
            _activationState.ClearOpenSettingsRequest();
        }
        else
        {
            NavigateByTag("privacy");
        }
    }

    private void RootNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            NavigateByTag(tag);
        }
    }

    public void NavigateByTag(string tag)
    {
        var pageType = tag switch
        {
            "privacy" => typeof(PrivacyPage),
            "performance" => typeof(PerformancePage),
            "network" => typeof(NetworkPage),
            "security" => typeof(SecurityPage),
            "search" => typeof(SearchPage),
            "appearance" => typeof(AppearancePage),
            "tweaks" => typeof(TweaksPage),
            "cleanup" => typeof(CleanupPage),
            "settings" => typeof(SettingsPage),
            _ => typeof(PrivacyPage)
        };

        if (ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }
    }

    private void ConfigureChrome()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        if (MicaController.IsSupported())
        {
            SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };
        }
    }
}
