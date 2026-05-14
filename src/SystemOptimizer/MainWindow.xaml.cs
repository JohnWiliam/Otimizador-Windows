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
    private readonly NavigationService _navigationService;
    private readonly StartupActivationState _activationState;

    public MainViewModel ViewModel { get; }

    public MainWindow(MainViewModel viewModel, NavigationService navigationService, StartupActivationState activationState)
    {
        ViewModel = viewModel;
        _navigationService = navigationService;
        _activationState = activationState;

        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SystemBackdrop = MicaController.IsSupported() ? new MicaBackdrop { Kind = MicaKind.BaseAlt } : null;
        RootNavigation.Header = ViewModel.ApplicationTitle;
        _navigationService.Initialize(ContentFrame);
        Activated += MainWindow_Activated;
        Closed += MainWindow_Closed;
    }

    private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= MainWindow_Activated;
        LoadingOverlay.Visibility = Visibility.Visible;
        Logger.Log("Inicialização WinUI 3 iniciada.");
        await ViewModel.InitializeAsync();
        LoadingOverlay.Visibility = Visibility.Collapsed;

        if (_activationState.OpenSettingsRequested)
        {
            NavigateTo(typeof(SettingsPage));
            RootNavigation.SelectedItem = RootNavigation.SettingsItem;
            _activationState.ClearOpenSettingsRequest();
        }
        else
        {
            SelectMenuItem("Privacy");
            NavigateTo(typeof(PrivacyPage));
        }
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        Activated -= MainWindow_Activated;
        Closed -= MainWindow_Closed;
        if (Application.Current is App app)
        {
            _ = app.ShutdownAsync();
        }
    }

    private void RootNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            NavigateTo(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag) return;

        var pageType = tag switch
        {
            "Privacy" => typeof(PrivacyPage),
            "Performance" => typeof(PerformancePage),
            "Network" => typeof(NetworkPage),
            "Security" => typeof(SecurityPage),
            "Search" => typeof(SearchPage),
            "Cleanup" => typeof(CleanupPage),
            "Appearance" => typeof(AppearancePage),
            "Tweaks" => typeof(TweaksPage),
            _ => typeof(PrivacyPage)
        };

        NavigateTo(pageType);
    }

    private void NavigateTo(Type pageType)
    {
        try
        {
            _navigationService.Navigate(pageType);
        }
        catch (Exception ex)
        {
            Logger.Log($"Falha de navegação WinUI para {pageType.Name}: {ex.Message}", "ERROR");
        }
    }

    private void SelectMenuItem(string tag)
    {
        foreach (var item in RootNavigation.MenuItems)
        {
            if (item is NavigationViewItem navigationItem && string.Equals(navigationItem.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                RootNavigation.SelectedItem = navigationItem;
                return;
            }
        }
    }
}
