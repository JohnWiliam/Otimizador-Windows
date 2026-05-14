using System;
using System.Globalization;
using System.Linq;
using System.Threading;
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

namespace SystemOptimizer;

public partial class App : Application
{
    private readonly IHost _host;
    private readonly string[] _args;
    private bool _isSilentMode;

    public static IServiceProvider Services => ((App)Current)._host.Services;
    public static MainWindow? Window { get; private set; }
    public static DispatcherQueue UiDispatcher { get; private set; } = null!;

    public App() : this(Environment.GetCommandLineArgs())
    {
    }

    public App(string[] args)
    {
        _args = args;
        UiDispatcher = DispatcherQueue.GetForCurrentThread();
        InitializeComponent();
        UnhandledException += OnUnhandledException;

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(ConfigureServices)
            .Build();
    }

    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
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
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<MainWindow>();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        await StartAsync(_args);
    }

    public async Task RunSilentModeWithoutUiAsync()
    {
        ApplyCulture();
        await _host.StartAsync();
        await RunSilentModeAsync();
        await _host.StopAsync();
        _host.Dispose();
    }

    private async Task StartAsync(string[] args)
    {
        _isSilentMode = args.Contains("--silent", StringComparer.OrdinalIgnoreCase);
        ApplyCulture();
        await _host.StartAsync();

        if (_isSilentMode)
        {
            await RunSilentModeAsync();
            Exit();
            return;
        }

        Services.GetRequiredService<StartupTasksService>().Initialize(args);
        Window = Services.GetRequiredService<MainWindow>();
        Window.Closed += async (_, _) =>
        {
            await _host.StopAsync();
            _host.Dispose();
        };
        Window.Activate();
    }

    private static void ApplyCulture()
    {
        AppSettings.Load();
        var culture = new CultureInfo(AppSettings.Current.Language);
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        Resources.Culture = culture;
    }

    public static void EnqueueOnUiThread(Action action)
    {
        if (action is null) return;
        if (!UiDispatcher.TryEnqueue(() => action()))
        {
            Logger.Log("Falha ao despachar ação para a thread de UI.", "WARNING");
        }
    }

    public static void ApplyTheme(ApplicationTheme theme)
    {
        if (Window?.Content is FrameworkElement root)
        {
            root.RequestedTheme = theme switch
            {
                ApplicationTheme.Light => ElementTheme.Light,
                ApplicationTheme.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Logger.Log($"Erro não tratado: {e.Exception}", "ERROR");
        e.Handled = true;
    }

    private async Task RunSilentModeAsync()
    {
        try
        {
            Logger.Log("Iniciando Modo Silencioso (Auto-Run)...");
            var tweakService = Services.GetRequiredService<TweakService>();
            var updateService = Services.GetRequiredService<IUpdateService>();
            tweakService.LoadTweaks();
            await tweakService.RefreshStatusesAsync();

            var savedTweakIds = TweakPersistence.LoadState();
            var appliedCount = 0;
            foreach (var id in savedTweakIds)
            {
                var tweak = tweakService.Tweaks.FirstOrDefault(t => t.Id == id);
                if (tweak != null && !tweak.IsOptimized)
                {
                    var result = tweak.Apply();
                    if (result.Success) appliedCount++;
                    else Logger.Log($"Falha ao aplicar {tweak.Id}: {result.Message}", "ERROR");
                }
            }
            Logger.Log($"Persistência concluída. {appliedCount} tweaks reaplicados.");
            await CheckForUpdatesAndNotifyAsync(updateService);
        }
        catch (Exception ex)
        {
            Logger.Log($"Erro crítico no modo silencioso: {ex}", "ERROR");
            if (_isSilentMode) Exit();
        }
    }

    private static async Task CheckForUpdatesAndNotifyAsync(IUpdateService updateService)
    {
        try
        {
            var updateInfo = await updateService.CheckForUpdatesAsync();
            if (updateInfo.IsAvailable) ShowUpdateToast(updateInfo);
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

        ToastCompatHelper.Show(toastBuilder);
    }
}
