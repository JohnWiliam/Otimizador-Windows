using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using System.Windows;
using CommunityToolkit.WinUI.Notifications; // CORRIGIDO
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
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly object _openSettingsLock = new();
    private DateTime _lastOpenSettingsRequestUtc = DateTime.MinValue;
    private bool _toastActivationRegistered;

    public StartupTasksService(
        IUpdateService updateService,
        INavigationService navigationService,
        StartupActivationState activationState,
        IHostApplicationLifetime applicationLifetime)
    {
        _updateService = updateService;
        _navigationService = navigationService;
        _activationState = activationState;
        _applicationLifetime = applicationLifetime;
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
        ToastCompatHelper.RegisterActivationHandler(argument =>
        {
            // Precisamos despachar para a UI Thread pois isso vem de um thread background
            Application.Current.Dispatcher.Invoke(() =>
            {
                HandleToastArguments(argument);
            });
        });

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
        if (Application.Current?.Dispatcher == null) return;

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (Application.Current.MainWindow is not { IsLoaded: true })
            {
                Logger.Log("Navegação para SettingsPage adiada até a janela principal estar carregada.");
                return;
            }

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
        var shutdownToken = _applicationLifetime.ApplicationStopping;
        _ = Task.Run(async () =>
        {
            try
            {
                shutdownToken.ThrowIfCancellationRequested();
                var updateInfo = await _updateService.CheckForUpdatesAsync();
                shutdownToken.ThrowIfCancellationRequested();

                if (updateInfo.IsAvailable)
                {
                    ShowUpdateToast(updateInfo);
                }
            }
            catch (OperationCanceledException) when (shutdownToken.IsCancellationRequested)
            {
                Logger.Log("Verificação de atualização em background cancelada durante encerramento.", "INFO");
            }
            catch (Exception ex)
            {
                Logger.Log($"Erro ao verificar atualizações em background: {ex.Message}", "ERROR");
            }
        }, shutdownToken);
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
