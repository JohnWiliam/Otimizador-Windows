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
using System.Diagnostics;
using System.Threading;
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

    [ObservableProperty]
    private int _cleanupProgressPercentage;

    [ObservableProperty]
    private string _cleanupProgressCategory = string.Empty;

    [ObservableProperty]
    private int _cleanupProcessedItems;

    public MainViewModel(TweakService tweakService, CleanupService cleanupService, IDialogService dialogService)
    {
        _tweakService = tweakService;
        _cleanupService = cleanupService;
        _dialogService = dialogService;

        _cleanupService.OnLogItem += (item) =>
        {
            Application.Current.Dispatcher.Invoke(() => CleanupLogs.Add(item));
        };

        _cleanupService.OnProgress += (progress) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CleanupProgressPercentage = progress.Percentage;
                CleanupProgressCategory = progress.CurrentCategory;
                CleanupProcessedItems = progress.ProcessedItems;
            });
        };
    }

    public Task<IReadOnlyList<CleanupCategoryResult>> RunCleanupScanAsync(CleanupOptions options, CancellationToken cancellationToken)
        => _cleanupService.RunScanAsync(options, cancellationToken);

    public Task RunSelectedCleanupAsync(CleanupOptions options, CancellationToken cancellationToken)
        => _cleanupService.RunCleanupAsync(options, cancellationToken);

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
        // CORRIGIDO: Apenas tweaks que REALMENTE requerem reinício do sistema
        // PF7: Agendamento GPU (HwSchMode) - configuração de driver de hardware
        // PF8: VBS/HVCI - segurança de hypervisor
        HashSet<string> rebootIds = ["PF7", "PF8"];
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

    // --- NOVOS COMANDOS (SearchPage) ---

    [RelayCommand]
    private async Task ApplySingle(string tweakId)
    {
        if (IsBusy) return;
        var vm = GetAllTweakViewModels().FirstOrDefault(x => x.Id == tweakId);
        if (vm == null) return;
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
    private async Task RestartExplorer()
    {
        try
        {
            foreach (var process in Process.GetProcessesByName("explorer"))
            {
                try { process.Kill(); } catch { }
            }
            Thread.Sleep(500);
            Process.Start("explorer.exe");
        }
        catch (Exception ex)
        {
            Logger.Log($"Falha ao reiniciar Explorer: {ex.Message}", "ERROR");
            await _dialogService.ShowMessageAsync(Resources.Msg_ErrorTitle, "Não foi possível reiniciar o Windows Explorer automaticamente.", DialogType.Error);
        }
    }

    // ----------------------------------------------------------------------

    private async Task ProcessTweaks(IEnumerable<TweakViewModel> list, bool applying)
    {
        int successCount = 0;
        int failCount = 0;
        string lastError = "";
        bool rebootNeeded = false;
        var selectedTweaks = list.Where(x => x.IsSelected).ToList();

        try
        {
            await Task.Run(() =>
            {
                foreach (var item in selectedTweaks)
                {
                    if (IsRebootRequired(item.Id)) rebootNeeded = true;
                    var result = applying ? item.Tweak.Apply() : item.Tweak.Revert();
                    if (result.Success) successCount++;
                    else { failCount++; lastError = result.Message; }
                }
            });

            if (selectedTweaks.Count > 0)
            {
                await _tweakService.RefreshStatusesAsync();
                TweakPersistence.SaveState(_tweakService.Tweaks);
            }

            foreach (var item in list) { item.IsSelected = false; item.UpdateStatusUI(); }
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
