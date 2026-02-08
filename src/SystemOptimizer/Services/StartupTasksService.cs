using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.WinUI.Notifications;
using SystemOptimizer.Helpers;
using SystemOptimizer.Views.Pages;
using Wpf.Ui.Abstractions;

namespace SystemOptimizer.Services;

public sealed class StartupTasksService
{
    private readonly IUpdateService _updateService;
    private readonly INavigationService _navigationService;
    private readonly StartupActivationState _activationState;
    private bool _toastActivationRegistered;

    public StartupTasksService(
        IUpdateService updateService,
        INavigationService navigationService,
        StartupActivationState activationState)
    {
        _updateService = updateService;
        _navigationService = navigationService;
        _activationState = activationState;
    }

    public void Initialize(string[] args)
    {
        RegisterToastActivation();
        ProcessActivationArguments(args);
        RunUpdateCheckInBackground();
    }

    public void ProcessActivationArguments(string[] args)
    {
        if (args.Any(arg => string.Equals(arg, "--open-settings", StringComparison.OrdinalIgnoreCase)))
        {
            RequestOpenSettings();
            return;
        }

        var protocolArg = args.FirstOrDefault(arg => arg.StartsWith("systemoptimizer://", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(protocolArg) && protocolArg.Contains("settings", StringComparison.OrdinalIgnoreCase))
        {
            RequestOpenSettings();
            return;
        }

        if (ToastNotificationManagerCompat.WasCurrentProcessToastActivated())
        {
            var toastArgs = ToastNotificationManagerCompat.GetToastActivationArgs();
            if (toastArgs != null)
            {
                HandleToastArguments(toastArgs.Argument);
            }
        }
    }

    private void RegisterToastActivation()
    {
        if (_toastActivationRegistered)
        {
            return;
        }

        ToastNotificationManagerCompat.OnActivated += toastArgs =>
        {
            HandleToastArguments(toastArgs.Argument);
        };

        _toastActivationRegistered = true;
    }

    private void HandleToastArguments(string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            return;
        }

        var args = ToastArguments.Parse(argument);
        if (args.TryGetValue("action", out var action) &&
            string.Equals(action, "open-settings", StringComparison.OrdinalIgnoreCase))
        {
            RequestOpenSettings();
        }
    }

    private void RequestOpenSettings()
    {
        _activationState.RequestOpenSettings();
        _ = TryNavigateToSettingsAsync();
    }

    private async Task TryNavigateToSettingsAsync()
    {
        if (Application.Current?.Dispatcher == null || Application.Current.MainWindow == null)
        {
            return;
        }

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                _navigationService.Navigate(typeof(SettingsPage));
            }
            catch (Exception ex)
            {
                Logger.Log($"Falha ao navegar para SettingsPage: {ex.Message}", "ERROR");
            }
        });
    }

    private void RunUpdateCheckInBackground()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var updateInfo = await _updateService.CheckForUpdatesAsync();
                if (updateInfo.IsAvailable)
                {
                    ShowUpdateToast(updateInfo);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Erro ao verificar atualizações em background: {ex.Message}", "ERROR");
            }
        });
    }

    private static void ShowUpdateToast(UpdateInfo updateInfo)
    {
        new ToastContentBuilder()
            .AddText("Atualização disponível")
            .AddText($"Versão {updateInfo.Version} disponível. Abra as configurações para atualizar.")
            .AddArgument("action", "open-settings")
            .Show(toast =>
            {
                toast.SuppressPopup = true;
            });
    }
}
