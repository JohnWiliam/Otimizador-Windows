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
    // DEPENDÊNCIA: Precisamos do TweakService para reaplicar as otimizações
    private readonly TweakService _tweakService;
    private bool _toastActivationRegistered;

    public StartupTasksService(
        IUpdateService updateService,
        INavigationService navigationService,
        StartupActivationState activationState,
        TweakService tweakService) // Injeção do TweakService
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

        // Só verifica atualizações se NÃO estivermos no modo silencioso (persistência)
        if (!IsSilentMode(args))
        {
            RunUpdateCheckInBackground();
        }
    }

    private bool IsSilentMode(string[] args)
    {
        return args.Any(arg => string.Equals(arg, "--silent", StringComparison.OrdinalIgnoreCase));
    }

    public void ProcessActivationArguments(string[] args)
    {
        // LÓGICA DE PERSISTÊNCIA ADICIONADA
        if (IsSilentMode(args))
        {
            // Executa a verificação em background para não travar a thread de UI inicial
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

            // 1. Carrega as definições dos tweaks
            _tweakService.LoadTweaks();

            // 2. Carrega quais tweaks o usuário salvou como ativos
            var savedIds = TweakPersistence.LoadState();
            
            if (savedIds.Count == 0)
            {
                Logger.Log("Nenhum estado de persistência encontrado. Encerrando.", "INFO");
            }
            else
            {
                int reappliedCount = 0;

                // 3. Itera sobre os tweaks salvos e verifica se precisam ser reaplicados
                foreach (var id in savedIds)
                {
                    var tweak = _tweakService.Tweaks.FirstOrDefault(t => t.Id == id);
                    if (tweak != null)
                    {
                        try
                        {
                            // Verifica o estado atual real no sistema
                            tweak.CheckStatus();

                            // Se deveria estar otimizado (está na lista savedIds) mas não está...
                            if (!tweak.IsOptimized)
                            {
                                Logger.Log($"Detectado tweak revertido: {tweak.Id}. Reaplicando...", "INFO");
                                
                                // CORREÇÃO AQUI: Capturamos o retorno da Tupla (Success, Message)
                                var result = tweak.Apply();
                                
                                if (result.Success)
                                {
                                    reappliedCount++;
                                }
                                else
                                {
                                    Logger.Log($"Falha ao reaplicar tweak {tweak.Id}: {result.Message}", "WARN");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Log($"Erro ao processar tweak {tweak.Id}: {ex.Message}", "ERROR");
                        }
                    }
                }
                
                Logger.Log($"Persistência concluída. {reappliedCount} tweaks foram reaplicados.", "INFO");
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Erro fatal durante a persistência: {ex.Message}", "ERROR");
        }
        finally
        {
            // IMPORTANTE: Encerra o aplicativo após a tarefa silenciosa
            // Usamos o Dispatcher pois Shutdown deve ser chamado na thread da UI
            Application.Current.Dispatcher.Invoke(() => 
            {
                Application.Current.Shutdown();
            });
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
