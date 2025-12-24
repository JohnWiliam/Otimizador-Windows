using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SystemOptimizer.Services;
using SystemOptimizer.ViewModels;
using SystemOptimizer.Helpers;
using SystemOptimizer.Views.Pages;
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
                
                // Registo do SettingsViewModel (A injeção do TweakService será automática aqui)
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
                services.AddTransient<CleanupPage>();
                services.AddTransient<AppearancePage>();
                
                services.AddTransient<SettingsPage>();
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
            // Agora aguardamos a execução completa do modo silencioso
            await RunSilentMode();
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

    private async Task RunSilentMode()
    {
        try
        {
            Logger.Log("Iniciando Modo Silencioso (Auto-Run)...");

            var tweakService = _host.Services.GetRequiredService<TweakService>();
            
            // 1. Carrega todos os tweaks disponíveis na memória
            tweakService.LoadTweaks();

            // 2. Verifica o estado atual real do sistema
            // (Isso é importante para não tentar aplicar algo que já está aplicado)
            await tweakService.RefreshStatusesAsync();

            // 3. Carrega a lista de desejos (o que estava ativo quando o user ativou a persistência)
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
                    
                    // Se o tweak existe E AINDA NÃO está otimizado no sistema atual
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
            // Fecha a aplicação após concluir o trabalho em background
            Shutdown();
        }
    }
}
