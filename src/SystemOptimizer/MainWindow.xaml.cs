using System.Windows;
using Wpf.Ui.Controls;
using Wpf.Ui;
using SystemOptimizer.ViewModels;
using System;

namespace SystemOptimizer
{
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
            DataContext = this;

            InitializeComponent();

            // Set the frame for navigation (FluentWindow does not contain a frame by default for NavigationView? 
            // In Wpf.Ui v3, NavigationView can create its own frame or we assign one. 
            // Actually NavigationView IS a ContentControl that can host a frame, or we use SetNavigationService logic)
            
            // Wpf.Ui v3 Pattern:
            navigationService.SetNavigationControl(RootNavigation);
            snackbarService.SetSnackbarPresenter(SnackbarPresenter);

            // Set the ServiceProvider so NavigationView can resolve pages via DI
            RootNavigation.SetServiceProvider(serviceProvider);
            
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await ViewModel.InitializeAsync();
            RootNavigation.Navigate(typeof(Views.Pages.PrivacyPage));
        }

        public INavigationView GetNavigation() => RootNavigation;
        public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);
        public void SetPageService(IPageService pageService) => RootNavigation.SetPageService(pageService);
        public void SetServiceProvider(IServiceProvider serviceProvider) => RootNavigation.SetServiceProvider(serviceProvider);
        public void ShowWindow() => Show();
        public void CloseWindow() => Close();
    }
}
