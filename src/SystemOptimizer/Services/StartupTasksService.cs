using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Toolkit.Uwp.Notifications; 
using SystemOptimizer.Helpers;
using SystemOptimizer.Views.Pages;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using System.Collections.Generic;

namespace SystemOptimizer.Services;

public sealed class StartupTasksService
{
    private readonly IUpdateService _updateService;
    private readonly INavigationService _navigationService;
    private readonly StartupActivationState _activationState;
    private readonly TweakService _tweakService;
    private bool _toastActivationRegistered;

    public StartupTasksService(
        IUpdateService updateService,
        INavigationService navigationService,
        StartupActivationState activationState,
        TweakService tweakService)
    {
        _updateService = updateService;
        _navigationService = navigationService;
        _activationState = activationState;
        _tweakService = tweakService;
    }

    public void Initialize(string[] args)
    {
        RegisterToastActivation();
        ProcessActivationArguments(args);

        // Se NÃO for modo silencioso, iniciamos a verificação em background (fire-and-forget)
        // Se FOR modo silencioso, a verificação será feita dentro de RunPersistenceCheckAsync
        if (!IsSilentMode(args))
        {
             _ = ExecuteUpdateCheckAsync();
        }
    }

    private bool IsSilentMode(string[] args)
    {
        return args.Any(arg => string.Equals(arg, "--silent", StringComparison.OrdinalIgnoreCase));
    }

    public void ProcessActivationArguments(string[] args)
    {
        if (IsSilentMode(args))
        {
            // Executa a verificação completa (Tweaks + Updates) e depois fecha
            Task.Run(async () => await RunPersistenceCheckAsync());
            return;
        }

        if (args.Any(arg => string.Equals(arg, "--open-settings", StringComparison.OrdinalIgnoreCase)))
        {
            RequestOpenSettings();
            return;
        }
    }

    private async Task RunPersistenceCheckAsync()
    {
        try
        {
            Logger.Log("Iniciando verificação de persistência (Modo Silencioso)...", "INFO");

            // 1. Inicia a verificação de UPDATE em paralelo (não bloqueia os tweaks)
            var updateTask = ExecuteUpdateCheckAsync();

            // 2. Lógica de persistência dos TWEAKS
            _tweakService.LoadTweaks();
            var savedIds = TweakPersistence.LoadState();
            
            if (savedIds.Count > 0)
            {
                int reappliedCount = 0;
                foreach (var id in savedIds)
                {
                    var tweak = _tweakService.Tweaks.FirstOrDefault(t => t.Id == id);
                    if (tweak != null)
                    {
                        try
                        {
                            tweak.CheckStatus();
                            if (!tweak.IsOptimized)
                            {
                                Logger.Log($"Detectado tweak revertido: {tweak.Id}. Reaplicando...", "INFO");
                                var result = tweak.Apply();
                                if (result.Success) reappliedCount++;
                            }
                        }
                        catch (Exception ex) 
                        { 
                            Logger.Log($"Erro ao processar tweak {tweak.Id}: {ex.Message}", "ERROR"); 
                        }
                    }
                }
                Logger.Log($"Persistência concluída. {reappliedCount} tweaks reaplicados.", "INFO");
            }

            // 3. Aguarda a verificação de UPDATE terminar antes de fechar o app
            // Isso garante que a notificação seja enviada se houver update
            await updateTask;
        }
        catch (Exception ex)
        {
            Logger.Log($"Erro fatal durante a persistência: {ex.Message}", "ERROR");
        }
        finally
        {
            // Encerra o processo para não ficar rodando em background
            Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
        }
    }

    // Método refatorado para retornar Task (pode ser aguardado)
    private async Task ExecuteUpdateCheckAsync()
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
            Logger.Log($"Erro ao verificar atualizações: {ex.Message}", "ERROR");
        }
    }

    private void RegisterToastActivation()
    {
        if (_toastActivationRegistered) return;

        ToastNotificationManagerCompat.OnActivated += toastArgs =>
        {
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
            }
            catch (Exception ex)
            {
                Logger.Log($"Falha ao navegar para SettingsPage: {ex.Message}", "ERROR");
            }
        });
    }

    private static void ShowUpdateToast(UpdateInfo updateInfo)
    {
        new ToastContentBuilder()
            .AddText("Atualização disponível")
            .AddText($"Versão {updateInfo.Version} disponível. Toque para atualizar.")
            .AddArgument("action", "open-settings")
            .Show();
    }
}
