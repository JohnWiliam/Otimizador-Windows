using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using SystemOptimizer.Helpers;
using SystemOptimizer.Services;
using SystemOptimizer.ViewModels;
using SystemOptimizer.Views.Pages;

namespace SystemOptimizer;

public partial class App : Application
{
    private readonly IHost _host;
    private readonly string[] _args;
    private bool _isSilentMode;

    public static IServiceProvider Services { get; private set; } = default!;
    public static MainWindow? MainWindowInstance { get; private set; }
    public static DispatcherQueue? UiDispatcherQueue { get; private set; }

    public App(string[] args)
    {
        _args = args;
        InitializeComponent();

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
                services.AddSingleton<IDialogService, DialogService>();

                services.AddSingleton<MainWindow>();
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

        Services = _host.Services;
        UnhandledException += OnUnhandledException;
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        UiDispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _isSilentMode = _args.Contains("--silent", StringComparer.OrdinalIgnoreCase);

        AppSettings.Load();
        ApplyCulture(AppSettings.Current.Language);

        try
        {
            await _host.StartAsync();

            if (_isSilentMode)
            {
                await RunSilentModeAsync();
                await ShutdownAsync();
                return;
            }

            MainWindowInstance = Services.GetRequiredService<MainWindow>();
            MainWindowInstance.Activate();

            Services.GetRequiredService<StartupTasksService>().Initialize(_args);
            await Services.GetRequiredService<MainViewModel>().InitializeAsync();
        }
        catch (Exception ex)
        {
            Logger.Log($"Startup failure: {ex}", "CRITICAL");
            throw;
        }
    }

    public static void DispatchToUi(Action action)
    {
        var dispatcher = UiDispatcherQueue;
        if (dispatcher is null || dispatcher.HasThreadAccess)
        {
            action();
            return;
        }

        dispatcher.TryEnqueue(() => action());
    }

    public async Task RunSilentModeWithoutUiAsync()
    {
        AppSettings.Load();
        ApplyCulture(AppSettings.Current.Language);
        await _host.StartAsync();
        await RunSilentModeAsync();
        await ShutdownAsync();
    }

    private async Task RunSilentModeAsync()
    {
        Logger.Log("Starting silent mode.");
        var tweakService = Services.GetRequiredService<TweakService>();
        tweakService.LoadTweaks();
        await tweakService.RefreshStatusesAsync();
        var persistedTweaks = TweakPersistence.LoadState().ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var tweak in tweakService.Tweaks.Where(t => persistedTweaks.Contains(t.Id)))
        {
            var result = tweak.Apply();
            Logger.Log($"Silent apply {tweak.Id}: success={result.Success} message={result.Message}");
        }
        Logger.Log("Silent mode finished.");
    }

    private async Task ShutdownAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
        Exit();
    }

    private static void ApplyCulture(string cultureName)
    {
        var culture = new CultureInfo(cultureName);
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        Properties.Resources.Culture = culture;
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Logger.Log($"Unhandled UI exception: {e.Exception}", "CRITICAL");
        e.Handled = true;
    }
}
