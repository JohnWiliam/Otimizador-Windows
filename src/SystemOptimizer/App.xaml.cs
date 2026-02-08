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
using System.Net.Http;
using CommunityToolkit.WinUI.Notifications;

namespace SystemOptimizer;

public partial class App : Application
{
    private readonly IHost _host;
    private bool _pendingOpenSettings;

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
                
                // NOVO: Registro do serviço de atualização
                services.AddSingleton<IUpdateService, UpdateService>();
                services.AddSingleton<StartupActivationState>();
                services.AddSingleton<StartupTasksService>();

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

        ToastNotificationManagerCompat.OnActivated += toastArgs =>
        {
            var arguments = ToastArguments.Parse(toastArgs.Argument);
            if (arguments.TryGetValue("action", out var action) && action == "open-settings")
            {
                Dispatcher.InvokeAsync(RequestOpenSettings);
            }
        };
    }

    public async Task RunSilentModeWithoutUiAsync()
    {
        AppSettings.Load();
        var culture = new System.Globalization.CultureInfo(AppSettings.Current.Language);
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        SystemOptimizer.Properties.Resources.Culture = culture;

        await _host.StartAsync();
        await RunSilentModeAsync();
        await _host.StopAsync();
        _host.Dispose();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        // Carrega configurações de idioma antes de inicializar a UI
        AppSettings.Load();
        var culture = new System.Globalization.CultureInfo(AppSettings.Current.Language);
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        // Atualiza a cultura do ResourceManager
        SystemOptimizer.Properties.Resources.Culture = culture;

        // Configura tratamento global de erros para evitar fechamento repentino
        this.DispatcherUnhandledException += OnDispatcherUnhandledException;

        await _host.StartAsync();

        if (e.Args.Contains("--open-settings", StringComparer.OrdinalIgnoreCase))
        {
            _pendingOpenSettings = true;
        }

        if (e.Args.Contains("--silent"))
        {
            // Agora aguardamos a execução completa do modo silencioso
            await RunSilentModeAsync();
            Shutdown();
        }
        else
        {
            var startupTasks = _host.Services.GetRequiredService<StartupTasksService>();
            startupTasks.Initialize(e.Args);
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
            if (_pendingOpenSettings)
            {
                NavigateToSettings(mainWindow);
                _pendingOpenSettings = false;
            }

            _ = _host.Services.GetRequiredService<StartupTasksService>().CheckForUpdatesAndNotifyAsync();
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

    private async Task RunSilentModeAsync()
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

            await _host.Services.GetRequiredService<StartupTasksService>()
                .CheckForUpdatesAndNotifyAsync();
        }
        catch (Exception ex)
        {
            Logger.Log($"Erro crítico no modo silencioso: {ex.Message}", "ERROR");
        }
    }

    private void RequestOpenSettings()
    {
        _pendingOpenSettings = true;

        try
        {
            var mainWindow = _host.Services.GetService<MainWindow>();
            if (mainWindow == null) return;
            if (!mainWindow.IsVisible)
            {
                mainWindow.Show();
            }
            NavigateToSettings(mainWindow);
            mainWindow.Activate();
            _pendingOpenSettings = false;
        }
        catch (Exception ex)
        {
            Logger.Log($"Erro ao abrir Configurações via notificação: {ex.Message}", "ERROR");
        }
    }

    private static void NavigateToSettings(MainWindow mainWindow)
    {
        mainWindow.Navigate(typeof(SettingsPage));
    }
}
