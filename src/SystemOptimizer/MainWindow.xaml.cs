using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SystemOptimizer.Services;
using SystemOptimizer.ViewModels;
using SystemOptimizer.Helpers;
using SystemOptimizer.Views.Pages;
using SystemOptimizer.Properties;
using Wpf.Ui;
using Wpf.Ui.Abstractions; 

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
                services.AddSingleton<SettingsViewModel>();
                services.AddTransient<TweakViewModel>();

                // 2. Core Services
                services.AddSingleton<TweakService>();
                services.AddSingleton<CleanupService>();

                // 3. UI Services
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
                services.AddTransient<SearchPage>();
                services.AddTransient<CleanupPage>();
                services.AddTransient<AppearancePage>();
                services.AddTransient<SettingsPage>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            // Carrega configurações de idioma antes de inicializar a UI
            AppSettings.Load();
            var culture = new System.Globalization.CultureInfo(AppSettings.Current.Language);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            // Atualiza a cultura do ResourceManager
            SystemOptimizer.Properties.Resources.Culture = culture;

            // Configura tratamento global de erros
            this.DispatcherUnhandledException += OnDispatcherUnhandledException;

            await _host.StartAsync();

            if (e.Args.Contains("--silent"))
            {
                await RunSilentMode();
            }
            else
            {
                // Resolve a MainWindow via DI (Isso falhará se StartupUri estiver definido no XAML)
                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                mainWindow.Show();
            }
        }
        catch (Exception ex)
        {
            // Captura erros fatais durante a inicialização (ex: falha no DI ou Resources)
            string errorMsg = $"Erro Fatal na Inicialização:\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
            MessageBox.Show(errorMsg, "Erro Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
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
        // Captura erros de execução na UI
        string errorMsg = $"Ocorreu um erro inesperado:\n{e.Exception.Message}\n\nOrigem: {e.Exception.Source}";
        Logger.Log(errorMsg, "ERROR");
        MessageBox.Show(errorMsg, "Erro do Sistema", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private async Task RunSilentMode()
    {
        try
        {
            Logger.Log("Iniciando Modo Silencioso (Auto-Run)...");

            var tweakService = _host.Services.GetRequiredService<TweakService>();
            
            // 1. Carrega todos os tweaks
            tweakService.LoadTweaks();

            // 2. Verifica estado atual
            await tweakService.RefreshStatusesAsync();

            // 3. Carrega persistência
            var savedTweakIds = TweakPersistence.LoadState();

            if (savedTweakIds.Count == 0)
            {
                Logger.Log("Nenhum tweak salvo para persistência.");
            }
            else
            {
                int appliedCount = 0;
                foreach (var id in savedTweakIds)
                {
                    var tweak = tweakService.Tweaks.FirstOrDefault(t => t.Id == id);
                    if (tweak != null && !tweak.IsOptimized)
                    {
                        Logger.Log($"Reaplicando tweak persistente: {tweak.Title} ({tweak.Id})");
                        var result = tweak.Apply();
                        if (result.Success) appliedCount++;
                        else Logger.Log($"Falha ao aplicar {tweak.Id}: {result.Message}", "ERROR");
                    }
                }
                Logger.Log($"Persistência concluída. {appliedCount} tweaks reaplicados.");
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Erro crítico no modo silencioso: {ex.Message}", "ERROR");
        }
        finally
        {
            Shutdown();
        }
    }
}
