using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Toolkit.Uwp.Notifications; // CORRIGIDO
using SystemOptimizer.Helpers;
using SystemOptimizer.Views.Pages;
using Wpf.Ui;
using Wpf.Ui.Abstractions;

namespace SystemOptimizer.Services;

public sealed class StartupTasksService
{
    private readonly IUpdateService _updateService;
    private readonly INavigationService _navigationService;
    private readonly StartupActivationState _activationState;
    private readonly object _openSettingsLock = new();
    private DateTime _lastOpenSettingsRequestUtc = DateTime.MinValue;
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
            Logger.Log("Argumento --open-settings detectado na inicialização.");
            RequestOpenSettings();
            return;
        }
    }

    private void RegisterToastActivation()
    {
        if (_toastActivationRegistered) return;

        Logger.Log("Registrando manipulador único de ativação por toast.");

        // Este evento dispara mesmo se o app foi aberto pelo Toast
        ToastNotificationManagerCompat.OnActivated += toastArgs =>
        {
            // Precisamos despachar para a UI Thread pois isso vem de um thread background
            Application.Current.Dispatcher.Invoke(() => 
            {
                HandleToastArguments(toastArgs.Argument);
            });
        };

        _toastActivationRegistered = true;
    }

    private void HandleToastArguments(string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument)) return;

        Logger.Log($"Evento de ativação de toast recebido: {argument}");

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
            if ((now - _lastOpenSettingsRequestUtc).TotalMilliseconds < 1000)
            {
                Logger.Log("Ação open-settings duplicada ignorada.");
                return;
            }

            _lastOpenSettingsRequestUtc = now;
        }

        Logger.Log("Ação open-settings recebida. Solicitando navegação.");
        _activationState.RequestOpenSettings();
        _ = TryNavigateToSettingsAsync();
    }

    private async Task TryNavigateToSettingsAsync()
    {
        if (Application.Current?.Dispatcher == null || Application.Current.MainWindow == null) return;

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                _navigationService.Navigate(typeof(SettingsPage));
                _activationState.ClearOpenSettingsRequest();
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
            .Show();
    }
}
