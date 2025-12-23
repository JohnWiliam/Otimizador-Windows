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
using SystemOptimizer.Helpers;
using Wpf.Ui;
using Wpf.Ui.Appearance;

namespace SystemOptimizer.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly TweakService _tweakService;
    private readonly CleanupService _cleanupService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private string _applicationTitle = "Otimizador de Sistema - Criado e Idealizado por John Wiliam & IA v2.0.0";

    public ObservableCollection<TweakViewModel> PrivacyTweaks { get; } = [];
    public ObservableCollection<TweakViewModel> PerformanceTweaks { get; } = [];
    public ObservableCollection<TweakViewModel> NetworkTweaks { get; } = [];
    public ObservableCollection<TweakViewModel> SecurityTweaks { get; } = [];
    public ObservableCollection<TweakViewModel> AppearanceTweaks { get; } = [];
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
            await _dialogService.ShowMessageAsync("Erro", $"Falha ao iniciar: {ex.Message}");
        }
        finally
        {
            IsInitializing = false;
        }
    }

    private IEnumerable<TweakViewModel> GetAllTweakViewModels()
    {
        return [..PrivacyTweaks, ..PerformanceTweaks, ..NetworkTweaks, ..SecurityTweaks, ..AppearanceTweaks, ..TweaksPageItems];
    }

    private void PopulateCategories()
    {
        PrivacyTweaks.Clear();
        PerformanceTweaks.Clear();
        NetworkTweaks.Clear();
        SecurityTweaks.Clear();
        AppearanceTweaks.Clear();
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
                case TweakCategory.Tweaks: TweaksPageItems.Add(vm); break;
            }
        }
    }

    private bool IsRebootRequired(string tweakId)
    {
        HashSet<string> rebootIds = ["PF4", "PF7", "PF8", "A1", "P1", "SE1"];
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
            "Tweaks" => TweaksPageItems,
            _ => []
        };

        await ProcessTweaks(list, true);
        IsBusy = false;
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
            "Tweaks" => TweaksPageItems,
            _ => []
        };

        await ProcessTweaks(list, false);
        IsBusy = false;
    }

    private async Task ProcessTweaks(IEnumerable<TweakViewModel> list, bool applying)
    {
        int successCount = 0;
        int failCount = 0;
        string lastError = "";
        bool rebootNeeded = false;

        await Task.Run(() =>
        {
            foreach (var item in list.Where(x => x.IsSelected))
            {
                if (IsRebootRequired(item.Id)) rebootNeeded = true;
                var result = applying ? item.Tweak.Apply() : item.Tweak.Revert();
                if (result.Success) successCount++;
                else { failCount++; lastError = result.Message; }
            }
        });

        foreach (var item in list) { item.IsSelected = false; item.UpdateStatusUI(); }

        if (failCount > 0)
        {
            await _dialogService.ShowMessageAsync("Resultado", $"Concluído com erros.\nSucessos: {successCount}\nFalhas: {failCount}\nErro: {lastError}");
        }
        else if (successCount > 0)
        {
            string msg = applying ? "Otimizações aplicadas." : "Configurações restauradas.";
            if (rebootNeeded) msg += "\n\nREINICIE o computador.";
            await _dialogService.ShowMessageAsync("Sucesso", msg);
        }
    }

    [RelayCommand]
    private async Task RunCleanup()
    {
        if (IsBusy) return;
        IsBusy = true;
        CleanupLogs.Clear();
        await _cleanupService.RunCleanupAsync();
        IsBusy = false;
    }
}
