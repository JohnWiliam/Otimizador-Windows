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
using System; // Necessário para AppContext

namespace SystemOptimizer;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    // O Host Genérico fornece injeção de dependência, configuração, logging, etc.
    private static readonly IHost _host = Host
        .CreateDefaultBuilder()
        // CORREÇÃO CRÍTICA: Usar AppContext.BaseDirectory em vez de Assembly.Location
        // Isso impede o erro de inicialização em builds de Arquivo Único (Single File)
        .ConfigureAppConfiguration(c => { c.SetBasePath(AppContext.BaseDirectory); })
        .ConfigureServices((context, services) =>
        {
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
            
            // --- Serviços da Correção de Pesquisa ---
            services.AddSingleton<SearchRegistryService>();
            // ----------------------------------------

            // Views and ViewModels
            services.AddSingleton<PrivacyPage>();
            services.AddSingleton<PerformancePage>();
            services.AddSingleton<NetworkPage>();
            services.AddSingleton<SecurityPage>();
            services.AddSingleton<AppearancePage>();
            services.AddSingleton<TweaksPage>();
            services.AddSingleton<CleanupPage>();
            services.AddSingleton<SettingsPage>();
            
            // --- Página da Correção de Pesquisa ---
            services.AddSingleton<SearchFixPage>();
            services.AddSingleton<SearchFixViewModel>();
            // --------------------------------------

            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<TweakViewModel>();
        }).Build();

    /// <summary>
    /// Gets registered service.
    /// </summary>
    public static T GetService<T>()
        where T : class
    {
        return (_host.Services.GetService(typeof(T)) as T)!;
    }

    /// <summary>
    /// Occurs when the application is loading.
    /// </summary>
    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();

        // Inicializa manualmente a janela principal
        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
    }

    /// <summary>
    /// Occurs when the application is closing.
    /// </summary>
    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();

        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Tratamento global de erros (opcional)
    }
}
