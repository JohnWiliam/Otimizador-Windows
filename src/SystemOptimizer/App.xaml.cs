using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

// Namespaces do projeto
using SystemOptimizer.Services;
using SystemOptimizer.ViewModels;
using SystemOptimizer.Views.Pages;
using SystemOptimizer.Helpers;

// Namespaces da biblioteca Wpf.Ui
using Wpf.Ui;
using Wpf.Ui.Abstractions; 

namespace SystemOptimizer;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static readonly IHost _host = Host
        .CreateDefaultBuilder()
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

            // --- CORREÇÃO: PageService ---
            // Regista o serviço concreto
            services.AddSingleton<PageService>();
            
            // Regista a interface IPageService (Necessária para o erro CS0246)
            services.AddSingleton<IPageService>(provider => provider.GetRequiredService<PageService>());

            // Regista a interface INavigationViewPageProvider
            services.AddSingleton<INavigationViewPageProvider>(provider => provider.GetRequiredService<PageService>());
            // -----------------------------

            // Services
            services.AddSingleton<TweakService>();
            services.AddSingleton<CleanupService>();
            services.AddSingleton<SearchRegistryService>(); // Certifique-se que SearchRegistryService.cs está na pasta Services

            // Views and ViewModels
            services.AddSingleton<PrivacyPage>();
            services.AddSingleton<PerformancePage>();
            services.AddSingleton<NetworkPage>();
            services.AddSingleton<SecurityPage>();
            services.AddSingleton<AppearancePage>();
            services.AddSingleton<TweaksPage>();
            services.AddSingleton<CleanupPage>();
            services.AddSingleton<SettingsPage>();
            
            // Search Fix Page (Certifique-se que os arquivos .cs e .xaml existem nas pastas corretas)
            services.AddSingleton<SearchFixPage>();
            services.AddSingleton<SearchFixViewModel>();

            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<TweakViewModel>();
        }).Build();

    public static T GetService<T>()
        where T : class
    {
        return (_host.Services.GetService(typeof(T)) as T)!;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();

        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Tratamento global de erros
    }
}
