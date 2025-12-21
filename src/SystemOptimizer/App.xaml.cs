using System;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SystemOptimizer.Services;
using SystemOptimizer.ViewModels;
using SystemOptimizer.Views.Pages;

namespace SystemOptimizer
{
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; } = null!;

        public App()
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            Services = ConfigureServices();
        }

        public static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddSingleton<TweakService>();
            services.AddSingleton<CleanupService>();
            services.AddSingleton<Wpf.Ui.IPageService, SystemOptimizer.Services.PageService>();
            services.AddSingleton<Wpf.Ui.INavigationService, Wpf.Ui.NavigationService>();
            services.AddSingleton<Wpf.Ui.ISnackbarService, Wpf.Ui.SnackbarService>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<MainViewModel>();

            services.AddSingleton<PrivacyPage>();
            services.AddSingleton<PerformancePage>();
            services.AddSingleton<NetworkPage>();
            services.AddSingleton<SecurityPage>();
            services.AddSingleton<AppearancePage>();
            services.AddSingleton<CleanupPage>();
            services.AddSingleton<TweaksPage>(); // Registrado como TweaksPage

            services.AddSingleton<MainWindow>();

            return services.BuildServiceProvider();
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"Ocorreu uma exceção: {e.Exception.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
