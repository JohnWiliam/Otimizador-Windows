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
    private readonly StartupActivationState _activationState;

    public MainWindow(
        MainViewModel viewModel,
        INavigationService navigationService,
        IServiceProvider serviceProvider,
        ISnackbarService snackbarService,
        IContentDialogService contentDialogService,
        StartupActivationState activationState) // Injetando o serviço de diálogo
    {
        ViewModel = viewModel;
        DataContext = ViewModel;
        _activationState = activationState;

        InitializeComponent();

        // ATIVA A SINCRONIZAÇÃO AUTOMÁTICA COM O TEMA DO WINDOWS
        SystemThemeWatcher.Watch(this);

        // Configura o controlo de navegação
        navigationService.SetNavigationControl(RootNavigation);
        snackbarService.SetSnackbarPresenter(SnackbarPresenter);
        
        // CORREÇÃO: SetContentPresenter foi substituído por SetDialogHost na versão mais recente
        contentDialogService.SetDialogHost(RootContentDialogPresenter);

        // INJETA O SERVICE PROVIDER NO NAVIGATIONVIEW
        RootNavigation.SetServiceProvider(serviceProvider);

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Logger.Log("MainWindow_Loaded started.");
        await ViewModel.InitializeAsync();
        Logger.Log("Initializing complete. Navigating to startup page...");
        if (_activationState.OpenSettingsRequested)
        {
            RootNavigation.Navigate(typeof(Views.Pages.SettingsPage));
            _activationState.ClearOpenSettingsRequest();
        }
        else
        {
            RootNavigation.Navigate(typeof(Views.Pages.PrivacyPage));
        }
        Logger.Log("Navigation call finished.");
    }

    public INavigationView GetNavigation() => RootNavigation;

    public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

    public void SetPageService(INavigationViewPageProvider pageService)
    {
        // Não é necessário fazer nada aqui na versão 4.x do WPF-UI
    }

    public void SetServiceProvider(IServiceProvider serviceProvider) => RootNavigation.SetServiceProvider(serviceProvider);

    public void ShowWindow() => Show();

    public void CloseWindow() => Close();
}
