using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SystemOptimizer.Services;
using SystemOptimizer.ViewModels;
using SystemOptimizer.Views.Pages;
using SystemOptimizer.Helpers;
using Wpf.Ui;

namespace SystemOptimizer;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static readonly IHost _host = Host
        .CreateDefaultBuilder()
        .ConfigureAppConfiguration(c => { c.SetBasePath(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!); })
        .ConfigureServices((context, services) =>
        {
            // Remover ApplicationHostService pois não está presente no projeto
            // services.AddHostedService<ApplicationHostService>();

            // Main window container with navigation
            services.AddSingleton<MainWindow>();
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<ISnackbarService, SnackbarService>();
            services.AddSingleton<IContentDialogService, ContentDialogService>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<SystemOptimizer.Services.PageService>();

            // Services
            services.AddSingleton<TweakService>();
            services.AddSingleton<CleanupService>();
            
            // --- NOVO: Search Fix Services ---
            services.AddSingleton<SearchRegistryService>();
            // ---------------------------------

            // Views and ViewModels
            services.AddSingleton<PrivacyPage>();
            services.AddSingleton<PerformancePage>();
            services.AddSingleton<NetworkPage>();
            services.AddSingleton<SecurityPage>();
            services.AddSingleton<AppearancePage>();
            services.AddSingleton<TweaksPage>();
            services.AddSingleton<CleanupPage>();
            services.AddSingleton<SettingsPage>();
            
            // --- NOVO: Search Fix View & ViewModel ---
            services.AddSingleton<SearchFixPage>();
            services.AddSingleton<SearchFixViewModel>();
            // -----------------------------------------

            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<TweakViewModel>();
        }).Build();

    /// <summary>
    /// Gets registered service.
    /// </summary>
    /// <typeparam name="T">Type of the service to get.</typeparam>
    /// <returns>Instance of the service or <see langword="null"/>.</returns>
    public static T GetService<T>()
        where T : class
    {
        return (_host.Services.GetService(typeof(T)) as T)!;
    }

    /// <summary>
    /// Occurs when the application is loading.
    /// </summary>
    private async void OnStartup(object sender, StartupEventArgs e)
    {
        await _host.StartAsync();

        // Inicializa manualmente a janela principal
        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    /// <summary>
    /// Occurs when the application is closing.
    /// </summary>
    private async void OnExit(object sender, ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
    }

    /// <summary>
    /// Occurs when an exception is thrown by an application but not handled.
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // For more info see https://docs.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception?view=netcore-3.0
    }
}
