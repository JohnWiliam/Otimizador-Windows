using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.WinUI.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using SystemOptimizer.Helpers;
using SystemOptimizer.Models;
using SystemOptimizer.Properties;
using SystemOptimizer.Services;
using SystemOptimizer.ViewModels;
using SystemOptimizer.Views.Pages;

namespace SystemOptimizer;

public partial class App : Application
{
    private readonly IHost _host;
    private Window? _window;
    private bool _isSilentMode;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<SettingsViewModel>();
                services.AddTransient<TweakViewModel>();

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
                services.AddSingleton<INavigationCoordinator, NavigationCoordinator>();
                services.AddSingleton<IXamlRootProvider, XamlRootProvider>();
                services.AddSingleton<IDialogService, DialogService>();

                services.AddTransient<MainWindow>();
                services.AddTransient<PrivacyPage>();
                services.AddTransient<PerformancePage>();
                services.AddTransient<NetworkPage>();
                services.AddTransient<SecurityPage>();
                services.AddTransient<SearchPage>();
                services.AddTransient<AppearancePage>();
                services.AddTransient<TweaksPage>();
                services.AddTransient<CleanupPage>();
                services.AddTransient<SettingsPage>();
            })
            .Build();
    }

    public static T GetService<T>() where T : notnull
        => ((App)Current)._host.Services.GetRequiredService<T>();

    public static DispatcherQueue UiDispatcherQueue => Current.DispatcherQueue;

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        await _host.StartAsync();

        var commandLineArgs = Environment.GetCommandLineArgs();
        _isSilentMode = commandLineArgs.Any(a => string.Equals(a, "--silent", StringComparison.OrdinalIgnoreCase));

        if (_isSilentMode)
        {
            await RunSilentModeAsync();
            Exit();
            return;
        }

        var startupTasks = _host.Services.GetRequiredService<StartupTasksService>();
        startupTasks.Initialize(commandLineArgs);

        _window = _host.Services.GetRequiredService<MainWindow>();
        _window.Activate();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Logger.Log($"Ocorreu um erro inesperado: {e.Exception}", "ERROR");
        e.Handled = true;
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
            var appliedCount = 0;

            foreach (var id in savedTweakIds)
            {
                var tweak = tweakService.Tweaks.FirstOrDefault(t => t.Id == id);
                if (tweak is null || tweak.IsOptimized) continue;

                Logger.Log($"Reaplicando tweak persistente: {tweak.Title} ({tweak.Id})");
                var result = tweak.Apply();
                if (result.Success) appliedCount++;
                else Logger.Log($"Falha ao aplicar {tweak.Id}: {result.Message}", "ERROR");
            }

            Logger.Log($"Persistência concluída. {appliedCount} tweaks reaplicados.");
            await CheckForUpdatesAndNotifyAsync(updateService);
        }
        catch (Exception ex)
        {
            Logger.Log($"Erro crítico no modo silencioso: {ex}", "ERROR");
        }
    }

    private static async Task CheckForUpdatesAndNotifyAsync(IUpdateService updateService)
    {
        try
        {
            var updateInfo = await updateService.CheckForUpdatesAsync();
            if (!updateInfo.IsAvailable) return;

            var toastBuilder = new ToastContentBuilder()
                .AddText("Atualização disponível")
                .AddText($"Versão {updateInfo.Version} disponível. Abra as configurações para atualizar.")
                .AddArgument("action", "open-settings");

            ToastCompatHelper.Show(toastBuilder);
        }
        catch (Exception ex)
        {
            Logger.Log($"Erro ao verificar atualizações no modo silencioso: {ex.Message}", "ERROR");
        }
    }
}
