using System;
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
using SystemOptimizer.Views.Pages;

namespace SystemOptimizer;

public partial class App : Application
{
    private readonly IHost _host;
    private bool _isSilentMode;
    private bool _hostDisposed;

    public static DispatcherQueue? UiDispatcherQueue { get; private set; }
    public static Window? MainAppWindow { get; private set; }
    public IServiceProvider Services => _host.Services;

    public App()
    {
        InitializeComponent();
        UiDispatcherQueue = DispatcherQueue.GetForCurrentThread();
        UnhandledException += OnUnhandledException;

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
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
                services.AddSingleton<NavigationService>();
                services.AddSingleton<IDialogService, DialogService>();

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

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        await StartAsync(Environment.GetCommandLineArgs().Skip(1).ToArray());
    }

    public async Task RunSilentModeWithoutUiAsync()
    {
        await ConfigureLocalizationAndHostAsync([]);
        await RunSilentModeAsync();
        await ShutdownHostAsync();
    }

    private async Task StartAsync(string[] args)
    {
        _isSilentMode = args.Contains("--silent", StringComparer.OrdinalIgnoreCase);
        await ConfigureLocalizationAndHostAsync(args);

        if (_isSilentMode)
        {
            try
            {
                await RunSilentModeAsync();
            }
            catch (Exception ex)
            {
                Logger.Log($"Falha ao iniciar modo silencioso: {ex}", "ERROR");
            }
            finally
            {
                await ShutdownHostAsync();
                Exit();
            }

            return;
        }

        var startupTasks = _host.Services.GetRequiredService<StartupTasksService>();
        startupTasks.Initialize(args);

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainAppWindow = mainWindow;
        mainWindow.Activate();
    }

    private async Task ConfigureLocalizationAndHostAsync(string[] args)
    {
        AppSettings.Load();
        var culture = new System.Globalization.CultureInfo(AppSettings.Current.Language);
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        SystemOptimizer.Properties.Resources.Culture = culture;
        await _host.StartAsync();
    }

    public async Task ShutdownAsync()
    {
        await ShutdownHostAsync();
    }

    private async Task ShutdownHostAsync()
    {
        if (_hostDisposed) return;

        await _host.StopAsync();
        _host.Dispose();
        _hostDisposed = true;
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Logger.Log($"Ocorreu um erro inesperado: {e.Exception}", "ERROR");
        e.Handled = true;
    }

    private async Task RunSilentModeAsync()
    {
        Logger.Log("Iniciando Modo Silencioso (Auto-Run)...");
        var tweakService = _host.Services.GetRequiredService<TweakService>();
        var updateService = _host.Services.GetRequiredService<IUpdateService>();

        tweakService.LoadTweaks();
        await tweakService.RefreshStatusesAsync();

        var savedStates = TweakPersistence.LoadState();
        foreach (var tweak in tweakService.Tweaks)
        {
            if (savedStates.TryGetValue(tweak.Id, out var shouldApply) && shouldApply)
            {
                var result = tweak.Apply();
                Logger.Log($"Silent apply {tweak.Id}: {result.Success} - {result.Message}");
            }
        }

        try
        {
            var updateInfo = await updateService.CheckForUpdatesAsync();
            if (updateInfo.IsAvailable)
            {
                ToastCompatHelper.Show(new ToastContentBuilder()
                    .AddText("Atualização disponível")
                    .AddText($"Versão {updateInfo.Version} disponível. Abra as configurações para atualizar.")
                    .AddArgument("action", "open-settings"));
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Falha ao verificar atualizações no modo silencioso: {ex.Message}", "WARNING");
        }
    }
}
