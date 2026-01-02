using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using SystemOptimizer.Models;
using SystemOptimizer.Services;
using System.Collections.Generic;
using System;
using System.Diagnostics; // Necessário para Process
using System.Threading;   // Necessário para Thread.Sleep
using SystemOptimizer.Helpers;
using SystemOptimizer.Properties;
using Wpf.Ui;
using Wpf.Ui.Appearance;

namespace SystemOptimizer.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly TweakService _tweakService;
    private readonly CleanupService _cleanupService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private string _applicationTitle = Resources.App_Title;

    public ObservableCollection<TweakViewModel> PrivacyTweaks { get; } = [];
    public ObservableCollection<TweakViewModel> PerformanceTweaks { get; } = [];
    public ObservableCollection<TweakViewModel> NetworkTweaks { get; } = [];
    public ObservableCollection<TweakViewModel> SecurityTweaks { get; } = [];
    public ObservableCollection<TweakViewModel> AppearanceTweaks { get; } = [];
    public ObservableCollection<TweakViewModel> SearchTweaks { get; } = [];
    public ObservableCollection<TweakViewModel> TweaksPageItems { get; } = [];

    public ObservableCollection<CleanupLogItem> CleanupLogs { get; } = [];

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isInitializing = true;

    public MainViewModel(TweakService tweakService, CleanupService cleanupService, IDialogService dialogService)
    {
        _tweakService = tweakService;
        _cleanupService = cleanupService;
        _dialogService = dialogService;

        _cleanupService.OnLogItem += (item) =>
        {
            Application.Current.Dispatcher.Invoke(() => CleanupLogs.Add(item));
        };
    }

    public async Task InitializeAsync()
    {
        IsInitializing = true;
        try
        {
            await Task.Run(() => { _tweakService.LoadTweaks(); });
            PopulateCategories();
            await _tweakService.RefreshStatusesAsync();
            foreach (var tweakVM in GetAllTweakViewModels()) { tweakVM.UpdateStatusUI(); }
        }
        catch (Exception ex)
        {
            Logger.Log($"Error during initialization: {ex.Message}", "CRITICAL");
            await _dialogService.ShowMessageAsync(Resources.Msg_ErrorTitle, string.Format(Resources.Msg_InitFail, ex.Message), DialogType.Error);
        }
        finally
        {
            IsInitializing = false;
        }
    }

    private IEnumerable<TweakViewModel> GetAllTweakViewModels()
    {
        return [..PrivacyTweaks, ..PerformanceTweaks, ..NetworkTweaks, ..SecurityTweaks, ..AppearanceTweaks, ..SearchTweaks, ..TweaksPageItems];
    }

    private void PopulateCategories()
    {
        PrivacyTweaks.Clear();
        PerformanceTweaks.Clear();
        NetworkTweaks.Clear();
        SecurityTweaks.Clear();
        AppearanceTweaks.Clear();
        SearchTweaks.Clear();
        TweaksPageItems.Clear();

        foreach (var tweak in _tweakService.Tweaks)
        {
            var vm = new TweakViewModel(tweak);
            switch (tweak.Category)
            {
                case TweakCategory.Privacy: PrivacyTweaks.Add(vm); break;
                case TweakCategory.Performance: PerformanceTweaks.Add(vm); break;
                case TweakCategory.Network: NetworkTweaks.Add(vm); break;
                case TweakCategory.Security: SecurityTweaks.Add(vm); break;
                case TweakCategory.Appearance: AppearanceTweaks.Add(vm); break;
                case TweakCategory.Search: SearchTweaks.Add(vm); break;
                case TweakCategory.Tweaks: TweaksPageItems.Add(vm); break;
            }
        }
    }

    private bool IsRebootRequired(string tweakId)
    {
        HashSet<string> rebootIds = ["PF4", "PF7", "PF8", "A1", "P1", "SE1", "SCH1", "SCH2", "SCH3"];
        return rebootIds.Contains(tweakId);
    }

    [RelayCommand]
    private async Task ApplySelected(string category)
    {
        if (IsBusy) return;
        IsBusy = true;

        IEnumerable<TweakViewModel> list = category switch
        {
            "Privacy" => PrivacyTweaks,
            "Performance" => PerformanceTweaks,
            "Network" => NetworkTweaks,
            "Security" => SecurityTweaks,
            "Appearance" => AppearanceTweaks,
            "Search" => SearchTweaks,
            "Tweaks" => TweaksPageItems,
            _ => []
        };

        await ProcessTweaks(list, true);
    }

    [RelayCommand]
    private async Task RevertSelected(string category)
    {
        if (IsBusy) return;
        IsBusy = true;

        IEnumerable<TweakViewModel> list = category switch
        {
            "Privacy" => PrivacyTweaks,
            "Performance" => PerformanceTweaks,
            "Network" => NetworkTweaks,
            "Security" => SecurityTweaks,
            "Appearance" => AppearanceTweaks,
            "Search" => SearchTweaks,
            "Tweaks" => TweaksPageItems,
            _ => []
        };

        await ProcessTweaks(list, false);
    }

    // --- NOVOS COMANDOS PARA A PÁGINA DE PESQUISA (Controle Individual) ---

    [RelayCommand]
    private async Task ApplySingle(string tweakId)
    {
        if (IsBusy) return;
        
        var vm = GetAllTweakViewModels().FirstOrDefault(x => x.Id == tweakId);
        if (vm == null) return;

        // Marca como selecionado apenas para o processamento, se necessário, 
        // ou passa diretamente para o ProcessTweaks (que filtra por IsSelected).
        // Aqui vamos forçar o processamento de apenas 1 item.
        vm.IsSelected = true; 
        
        IsBusy = true;
        await ProcessTweaks(new[] { vm }, true);
    }

    [RelayCommand]
    private async Task RevertSingle(string tweakId)
    {
        if (IsBusy) return;

        var vm = GetAllTweakViewModels().FirstOrDefault(x => x.Id == tweakId);
        if (vm == null) return;

        vm.IsSelected = true;
        
        IsBusy = true;
        await ProcessTweaks(new[] { vm }, false);
    }

    [RelayCommand]
    private void RestartExplorer()
    {
        try
        {
            foreach (var process in Process.GetProcessesByName("explorer"))
            {
                try { process.Kill(); } catch { }
            }
            
            // Pequena pausa para garantir que o processo morreu
            Thread.Sleep(500);

            // Reinicia
            Process.Start("explorer.exe");
        }
        catch (Exception ex)
        {
            Logger.Log($"Falha ao reiniciar Explorer: {ex.Message}", "ERROR");
            _dialogService.ShowMessageAsync("Erro", "Não foi possível reiniciar o Windows Explorer automaticamente.", DialogType.Error);
        }
    }

    // ----------------------------------------------------------------------

    private async Task ProcessTweaks(IEnumerable<TweakViewModel> list, bool applying)
    {
        int successCount = 0;
        int failCount = 0;
        string lastError = "";
        bool rebootNeeded = false;

        try
        {
            await Task.Run(() =>
            {
                // Processa apenas os selecionados (ou passados manualmente como selecionados)
                foreach (var item in list.Where(x => x.IsSelected))
                {
                    if (IsRebootRequired(item.Id)) rebootNeeded = true;
                    var result = applying ? item.Tweak.Apply() : item.Tweak.Revert();
                    if (result.Success) successCount++;
                    else { failCount++; lastError = result.Message; }
                }
            });

            // Atualiza UI
            foreach (var item in list) 
            { 
                item.IsSelected = false; 
                item.UpdateStatusUI(); 
            }
        }
        finally
        {
            IsBusy = false;
        }

        if (failCount > 0)
        {
            await _dialogService.ShowMessageAsync(Resources.Msg_ResultTitle, string.Format(Resources.Msg_CompletedWithErrors, successCount, failCount, lastError), DialogType.Warning);
        }
        else if (successCount > 0)
        {
            string msg = applying ? Resources.Msg_Applied : Resources.Msg_Restored;
            if (rebootNeeded) msg += Resources.Msg_RebootNeeded;
            
            await _dialogService.ShowMessageAsync(Resources.Msg_SuccessTitle, msg, DialogType.Success);
        }
    }

    [RelayCommand]
    private async Task RunCleanup()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            CleanupLogs.Clear();
            await _cleanupService.RunCleanupAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }
}
