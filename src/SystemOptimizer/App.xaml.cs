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

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Services
            services.AddSingleton<TweakService>();
            services.AddSingleton<CleanupService>();
            services.AddSingleton<Wpf.Ui.IPageService, SystemOptimizer.Services.PageService>();
            services.AddSingleton<Wpf.Ui.INavigationService, Wpf.Ui.NavigationService>();
            services.AddSingleton<Wpf.Ui.ISnackbarService, Wpf.Ui.SnackbarService>();
            services.AddSingleton<IDialogService, DialogService>();

            // ViewModels
            services.AddSingleton<MainViewModel>();

            // Pages
            services.AddSingleton<PrivacyPage>();
            services.AddSingleton<PerformancePage>();
            services.AddSingleton<NetworkPage>();
            services.AddSingleton<SecurityPage>();
            services.AddSingleton<AppearancePage>();
            services.AddSingleton<CleanupPage>();

            // Windows
            services.AddSingleton<MainWindow>();

            return services.BuildServiceProvider();
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"Ocorreu uma exceção não tratada: {e.Exception.Message}\n\nStack Trace:\n{e.Exception.StackTrace}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
