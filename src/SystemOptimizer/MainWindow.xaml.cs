using System;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SystemOptimizer.Helpers;
using SystemOptimizer.Services;
using SystemOptimizer.ViewModels;
using SystemOptimizer.Views.Pages;

namespace SystemOptimizer;

public sealed partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly StartupActivationState _activationState;
    private readonly INavigationCoordinator _navigationCoordinator;
    private readonly IXamlRootProvider _xamlRootProvider;
    private bool _isInitialized;

    public MainWindow(
        MainViewModel viewModel,
        StartupActivationState activationState,
        INavigationCoordinator navigationCoordinator,
        IXamlRootProvider xamlRootProvider)
    {
        _viewModel = viewModel;
        _activationState = activationState;
        _navigationCoordinator = navigationCoordinator;
        _xamlRootProvider = xamlRootProvider;

        InitializeComponent();
        Title = _viewModel.ApplicationTitle;
        SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };
        ExtendsContentIntoTitleBar = true;

        _navigationCoordinator.Initialize(ContentFrame);
        _xamlRootProvider.Initialize(ContentFrame);
        Activated += OnActivated;
        Closed += (_, _) => _xamlRootProvider.Clear();
    }

    private async void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (_isInitialized) return;
        _isInitialized = true;

        Logger.Log("Inicializando experiência WinUI 3 nativa.");
        await _viewModel.InitializeAsync();
        InitializingOverlay.Visibility = Visibility.Collapsed;

        Navigate(_activationState.OpenSettingsRequested ? "settings" : "privacy");
        _activationState.ClearOpenSettingsRequest();
    }

    private void RootNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is string tag)
        {
            Navigate(tag);
        }
    }

    private void Navigate(string tag)
    {
        Type pageType = tag switch
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

        ContentFrame.Navigate(pageType);
    }
}
