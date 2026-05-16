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
using SystemOptimizer.Models;

namespace SystemOptimizer;

public partial class App : Application
{
    private readonly IHost _host;
    private readonly SemaphoreSlim _hostLifecycleLock = new(1, 1);
    private bool _isSilentMode;
    private bool _hostStarted;
    private bool _hostDisposed;

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
                services.AddSingleton<CleanupExecutionEngine>();
                services.AddSingleton<ICleanupTargetProvider, UserTempCleanupTargetProvider>();
                services.AddSingleton<ICleanupTargetProvider, SystemTempCleanupTargetProvider>();
                services.AddSingleton<ICleanupTargetProvider, PrefetchCleanupTargetProvider>();
                services.AddSingleton<ICleanupTargetProvider, BrowserCacheCleanupTargetProvider>();
                services.AddSingleton<ICleanupTargetProvider, DnsCleanupTargetProvider>();
                services.AddSingleton<ICleanupTargetProvider, WindowsUpdateCleanupTargetProvider>();
                services.AddSingleton<ICleanupTargetProvider, RecycleBinCleanupTargetProvider>();
                services.AddSingleton<CleanupService>();
                services.AddSingleton<IUpdateService, UpdateService>();
                services.AddSingleton<StartupActivationState>();
                services.AddSingleton<StartupTasksService>();

                // 3. UI Services
                services.AddSingleton<Wpf.Ui.Abstractions.INavigationViewPageProvider, PageService>();
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

    public async Task RunSilentModeWithoutUiAsync()
    {
        AppSettings.Load();
        var culture = new System.Globalization.CultureInfo(AppSettings.Current.Language);
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        SystemOptimizer.Properties.Resources.Culture = culture;

        await EnsureHostStartedAsync();
        try
        {
            await RunSilentModeAsync();
        }
        finally
        {
            await StopHostAsync();
        }
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            await OnStartupAsync(e);
        }
        catch (Exception ex)
        {
            Logger.Log($"Falha fatal ao iniciar aplicação: {ex}", "ERROR");
            Shutdown(_isSilentMode ? 1 : 0);
        }
    }

    private async Task OnStartupAsync(StartupEventArgs e)
    {
        _isSilentMode = e.Args.Contains("--silent", StringComparer.OrdinalIgnoreCase);

        AppSettings.Load();
        var culture = new System.Globalization.CultureInfo(AppSettings.Current.Language);
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        SystemOptimizer.Properties.Resources.Culture = culture;

        this.DispatcherUnhandledException += OnDispatcherUnhandledException;

        await EnsureHostStartedAsync();

        if (_isSilentMode)
        {
            try
            {
                await RunSilentModeAsync();
                Shutdown();
            }
            catch (Exception ex)
            {
                Logger.Log($"Falha ao iniciar modo silencioso: {ex}", "ERROR");
                Shutdown(1);
            }
        }
        else
        {
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
            var startupTasks = _host.Services.GetRequiredService<StartupTasksService>();
            startupTasks.Initialize(e.Args);
        }

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        StopHostAsync().GetAwaiter().GetResult();
        base.OnExit(e);
    }

    private async Task EnsureHostStartedAsync()
    {
        await _hostLifecycleLock.WaitAsync();
        try
        {
            if (_hostDisposed)
            {
                throw new ObjectDisposedException(nameof(_host), "Host já foi finalizado.");
            }

            if (_hostStarted)
            {
                return;
            }

            await _host.StartAsync();
            _hostStarted = true;
        }
        finally
        {
            _hostLifecycleLock.Release();
        }
    }

    private async Task StopHostAsync()
    {
        await _hostLifecycleLock.WaitAsync();
        try
        {
            if (_hostDisposed) return;

            try
            {
                if (_hostStarted)
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await _host.StopAsync(cts.Token);
                    _hostStarted = false;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Erro ao parar host: {ex.Message}", "ERROR");
            }
            finally
            {
                _host.Dispose();
                _hostDisposed = true;
            }
        }
        finally
        {
            _hostLifecycleLock.Release();
        }
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        string errorMsg = $"Ocorreu um erro inesperado: {e.Exception}";
        Logger.Log(errorMsg, "ERROR");

        if (!_isSilentMode)
        {
            MessageBox.Show(
                $"Ocorreu um erro inesperado: {e.Exception.Message}",
                "Erro do Sistema",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        e.Handled = true;

        if (_isSilentMode)
        {
            Shutdown(1);
        }
    }

    private async Task RunSilentModeAsync()
    {
        try
        {
            Logger.Log("Iniciando Modo Silencioso (Auto-Run)...");
            var tweakService = _host.Services.GetRequiredService<TweakService>();
            var updateService = _host.Services.GetRequiredService<IUpdateService>();
            tweakService.LoadTweaks();
            await tweakService.RefreshStatusesAsync();

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

            await CheckForUpdatesAndNotifyAsync(updateService);
        }
        catch (Exception ex)
        {
            Logger.Log($"Erro crítico no modo silencioso: {ex}", "ERROR");
            if (_isSilentMode)
            {
                Shutdown(1);
            }
        }
    }

    private static async Task CheckForUpdatesAndNotifyAsync(IUpdateService updateService)
    {
        try
        {
            var updateInfo = await updateService.CheckForUpdatesAsync();
            if (!updateInfo.IsAvailable)
            {
                Logger.Log("Modo silencioso: nenhuma atualização encontrada.");
                return;
            }

            ShowUpdateToast(updateInfo);
            Logger.Log($"Modo silencioso: atualização {updateInfo.Version} detectada e notificação exibida.");
        }
        catch (Exception ex)
        {
            Logger.Log($"Erro ao verificar atualizações no modo silencioso: {ex.Message}", "ERROR");
        }
    }

    private static void ShowUpdateToast(UpdateInfo updateInfo)
    {
        var toastBuilder = new ToastContentBuilder()
            .AddText("Atualização disponível")
            .AddText($"Versão {updateInfo.Version} disponível. Abra as configurações para atualizar.")
            .AddArgument("action", "open-settings");

        Helpers.ToastCompatHelper.Show(toastBuilder);
    }

}
