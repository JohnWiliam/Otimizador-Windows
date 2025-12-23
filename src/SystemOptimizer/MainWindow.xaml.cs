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

        // Configura o controlo de navegação
        navigationService.SetNavigationControl(RootNavigation);
        snackbarService.SetSnackbarPresenter(SnackbarPresenter);

        // INJETA O SERVICE PROVIDER NO NAVIGATIONVIEW
        // Isso permite que o controlo resolva as páginas e o INavigationViewPageProvider automaticamente.
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

    // CORREÇÃO: O método SetPageService foi removido do NavigationView na v4.
    // Como já usamos SetServiceProvider no construtor, o NavigationView já consegue resolver as páginas.
    // Mantemos este método apenas para satisfazer a interface INavigationWindow.
    public void SetPageService(INavigationViewPageProvider pageService)
    {
        // Não é necessário fazer nada aqui na versão 4.x do WPF-UI
    }

    public void SetServiceProvider(IServiceProvider serviceProvider) => RootNavigation.SetServiceProvider(serviceProvider);

    public void ShowWindow() => Show();

    public void CloseWindow() => Close();
}
