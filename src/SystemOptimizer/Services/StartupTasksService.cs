using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.WinUI.Notifications;
using SystemOptimizer.Helpers;

namespace SystemOptimizer.Services;

public sealed class StartupTasksService
{
    private readonly IUpdateService _updateService;
    private readonly StartupActivationState _activationState;
    private readonly object _openSettingsLock = new();
    private DateTime _lastOpenSettingsRequestUtc = DateTime.MinValue;
    private bool _toastActivationRegistered;

    public StartupTasksService(IUpdateService updateService, StartupActivationState activationState)
    {
        _updateService = updateService;
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
        }
    }

    private void RegisterToastActivation()
    {
        if (_toastActivationRegistered) return;
        ToastCompatHelper.RegisterActivationHandler(argument => App.EnqueueOnUiThread(() => HandleToastArguments(argument)));
        _toastActivationRegistered = true;
    }

    private void HandleToastArguments(string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument)) return;

        try
        {
            var args = ToastArguments.Parse(argument);
            if (args.TryGetValue("action", out var action) &&
                string.Equals(action, "open-settings", StringComparison.OrdinalIgnoreCase))
            {
                RequestOpenSettings();
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Erro ao processar argumentos do toast: {ex.Message}", "ERROR");
        }
    }

    private void RequestOpenSettings()
    {
        lock (_openSettingsLock)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastOpenSettingsRequestUtc).TotalMilliseconds < 1000) return;
            _lastOpenSettingsRequestUtc = now;
        }

        _activationState.RequestOpenSettings();
        App.EnqueueOnUiThread(() =>
        {
            App.Window?.NavigateByTag("settings");
            _activationState.ClearOpenSettingsRequest();
        });
    }

    private void RunUpdateCheckInBackground()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var updateInfo = await _updateService.CheckForUpdatesAsync();
                if (updateInfo.IsAvailable) ShowUpdateToast(updateInfo);
            }
            catch (Exception ex)
            {
                Logger.Log($"Erro ao verificar atualizações em background: {ex.Message}", "ERROR");
            }
        });
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
