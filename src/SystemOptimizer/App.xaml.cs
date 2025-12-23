using System;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SystemOptimizer.Services;
using SystemOptimizer.ViewModels;
using SystemOptimizer.Helpers;
using SystemOptimizer.Views.Pages;
using Wpf.Ui;
using Wpf.Ui.Abstractions; // Adicionado para corrigir o erro CS0246

namespace SystemOptimizer;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // 1. ViewModels
                services.AddSingleton<MainViewModel>();
                services.AddTransient<TweakViewModel>();

                // 2. Core Services
                services.AddSingleton<TweakService>();
                services.AddSingleton<CleanupService>();

                // 3. UI Services
                // CORREÇÃO: IPageService substituído por INavigationViewPageProvider
                services.AddSingleton<INavigationViewPageProvider, PageService>();
                
                services.AddSingleton<INavigationService, NavigationService>();
                services.AddSingleton<IDialogService, DialogService>();
                services.AddSingleton<ISnackbarService, SnackbarService>();
                services.AddSingleton<IContentDialogService, ContentDialogService>();

                // 4. Windows & Pages
                services.AddSingleton<MainWindow>();
                services.AddTransient<TweaksPage>();
                services.AddTransient<PerformancePage>();
                services.AddTransient<PrivacyPage>();
                services.AddTransient<NetworkPage>();
                services.AddTransient<SecurityPage>();
                services.AddTransient<CleanupPage>();
                services.AddTransient<AppearancePage>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        // Configura tratamento global de erros para evitar fechamento repentino
        this.DispatcherUnhandledException += OnDispatcherUnhandledException;

        await _host.StartAsync();

        if (e.Args.Contains("--silent"))
        {
            RunSilentMode();
        }
        else
        {
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        // Captura erros que acontecerem na UI (ex: clique de botão que falha)
        string errorMsg = $"Ocorreu um erro inesperado: {e.Exception.Message}";
        Logger.Log(errorMsg, "ERROR");
        MessageBox.Show(errorMsg, "Erro do Sistema", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true; // Impede o crash total se possível
    }

    private void RunSilentMode()
    {
        try
        {
            Logger.Log("Iniciando Modo Silencioso...");

            var tweakService = _host.Services.GetRequiredService<TweakService>();
            tweakService.LoadTweaks();

            // Lógica adicional de persistência se necessário
            Logger.Log("Modo Silencioso Concluído.");
        }
        catch (Exception ex)
        {
            Logger.Log($"Erro no modo silencioso: {ex.Message}", "ERROR");
        }
        finally
        {
            Shutdown();
        }
    }
}
