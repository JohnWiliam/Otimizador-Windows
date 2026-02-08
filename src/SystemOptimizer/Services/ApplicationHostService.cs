using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SystemOptimizer.Views.Pages;
using Wpf.Ui;

namespace SystemOptimizer.Services
{
    /// <summary>
    /// Managed host of the application.
    /// </summary>
    public class ApplicationHostService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly INavigationService _navigationService;
        private readonly StartupTasksService _startupTasksService;

        public ApplicationHostService(
            IServiceProvider serviceProvider,
            INavigationService navigationService,
            StartupTasksService startupTasksService)
        {
            _serviceProvider = serviceProvider;
            _navigationService = navigationService;
            _startupTasksService = startupTasksService;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await HandleActivationAsync();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
        }

        private async Task HandleActivationAsync()
        {
            await Task.CompletedTask;

            if (Application.Current.Windows.OfType<MainWindow>().Any())
            {
                return;
            }

            var navigationWindow = _serviceProvider.GetRequiredService<MainWindow>();
            navigationWindow.Loaded += OnNavigationWindowLoaded;
            navigationWindow.Show();
        }

        private void OnNavigationWindowLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is not MainWindow navigationWindow)
            {
                return;
            }

            navigationWindow.NavigationView.Navigate(typeof(PrivacyPage));
        }
    }
}