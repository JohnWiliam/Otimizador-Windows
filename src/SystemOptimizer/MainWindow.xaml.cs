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

    // CORREÇÃO: Propriedade pública necessária para o ApplicationHostService acessar a navegação
    public INavigationView NavigationView => RootNavigation;

    public MainWindow(
        MainViewModel viewModel,
        INavigationService navigationService,
        IServiceProvider serviceProvider,
        ISnackbarService snackbarService,
        IContentDialogService contentDialogService,
        StartupActivationState activationState)
    {
        ViewModel = viewModel;
        DataContext = ViewModel;
        _activationState = activationState;

        InitializeComponent();

        SystemThemeWatcher.Watch(this);

        // Configuração dos serviços de UI
        navigationService.SetNavigationControl(RootNavigation);
        snackbarService.SetSnackbarPresenter(SnackbarPresenter);
        contentDialogService.SetDialogHost(RootContentDialogPresenter);

        // Injeção do ServiceProvider para resolução de páginas
        RootNavigation.SetServiceProvider(serviceProvider);

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Logger.Log("MainWindow_Loaded started.");
        await ViewModel.InitializeAsync();
        
        Logger.Log("Verificando requisições de navegação inicial...");
        if (_activationState.OpenSettingsRequested)
        {
            RootNavigation.Navigate(typeof(Views.Pages.SettingsPage));
            _activationState.ClearOpenSettingsRequest();
        }
        else
        {
            RootNavigation.Navigate(typeof(Views.Pages.PrivacyPage));
        }
        Logger.Log("Navegação inicial concluída.");
    }

    // Métodos da interface INavigationWindow
    public INavigationView GetNavigation() => RootNavigation;

    public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

    public void SetPageService(INavigationViewPageProvider pageService)
    {
        // Método exigido pela interface, mas não utilizado diretamente nesta implementação
    }

    public void SetServiceProvider(IServiceProvider serviceProvider) => RootNavigation.SetServiceProvider(serviceProvider);

    public void ShowWindow() => Show();

    public void CloseWindow() => Close();
}