using System;
using System.Windows;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using SystemOptimizer.Helpers;
using SystemOptimizer.ViewModels;
using SystemOptimizer.Services;

namespace SystemOptimizer;

public partial class MainWindow : FluentWindow, INavigationWindow
{
    public MainViewModel ViewModel { get; }

    public MainWindow(
        MainViewModel viewModel,
        INavigationService navigationService,
        IServiceProvider serviceProvider,
        ISnackbarService snackbarService)
    {
        ViewModel = viewModel;
        DataContext = ViewModel;

        InitializeComponent();

        // ATIVA A SINCRONIZAÇÃO AUTOMÁTICA COM O TEMA DO WINDOWS
        SystemThemeWatcher.Watch(this);

        // Initialize the navigation service with the NavigationView control
        navigationService.SetNavigationControl(RootNavigation);
        snackbarService.SetSnackbarPresenter(SnackbarPresenter);

        // Set the ServiceProvider so NavigationView can resolve pages via DI
        RootNavigation.SetServiceProvider(serviceProvider);

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Logger.Log("MainWindow_Loaded started.");
        await ViewModel.InitializeAsync();
        Logger.Log("Initializing complete. Navigating to PrivacyPage...");
        RootNavigation.Navigate(typeof(Views.Pages.PrivacyPage));
        Logger.Log("Navigation call finished.");
    }

    public INavigationView GetNavigation() => RootNavigation;

    public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

    // CORREÇÃO: Uso de cast explícito para INavigationView para acessar SetPageService
    public void SetPageService(INavigationViewPageProvider pageService) => ((INavigationView)RootNavigation).SetPageService(pageService);

    public void SetServiceProvider(IServiceProvider serviceProvider) => RootNavigation.SetServiceProvider(serviceProvider);

    public void ShowWindow() => Show();

    public void CloseWindow() => Close();
}
