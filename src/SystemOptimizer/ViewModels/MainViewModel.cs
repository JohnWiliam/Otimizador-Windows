using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using SystemOptimizer.Models;
using SystemOptimizer.Services;
using System.Collections.Generic;

namespace SystemOptimizer.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly TweakService _tweakService;
        private readonly CleanupService _cleanupService;
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private string _applicationTitle = "Otimizador de Sistema - Criado por John Wiliam & IA";

        public ObservableCollection<TweakViewModel> PrivacyTweaks { get; } = new();
        public ObservableCollection<TweakViewModel> PerformanceTweaks { get; } = new();
        public ObservableCollection<TweakViewModel> NetworkTweaks { get; } = new();
        public ObservableCollection<TweakViewModel> SecurityTweaks { get; } = new();
        public ObservableCollection<TweakViewModel> AppearanceTweaks { get; } = new();

        public ObservableCollection<CleanupLogItem> CleanupLogs { get; } = new();

        [ObservableProperty]
        private bool _isBusy;
        
        [ObservableProperty]
        private bool _isInitializing = true;

        // Constructor for DI
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
            // Executa o carregamento em background para não travar a UI inicial
            await Task.Run(() => 
            {
                _tweakService.LoadTweaks();
            });
            
            PopulateCategories();
            
            await _tweakService.RefreshStatusesAsync();
            foreach (var tweakVM in GetAllTweakViewModels())
            {
                tweakVM.UpdateStatusUI();
            }
            IsInitializing = false;
        }

        private IEnumerable<TweakViewModel> GetAllTweakViewModels()
        {
            return PrivacyTweaks.Concat(PerformanceTweaks)
                                .Concat(NetworkTweaks)
                                .Concat(SecurityTweaks)
                                .Concat(AppearanceTweaks);
        }

        private void PopulateCategories()
        {
            PrivacyTweaks.Clear();
            PerformanceTweaks.Clear();
            NetworkTweaks.Clear();
            SecurityTweaks.Clear();
            AppearanceTweaks.Clear();

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
                }
            }
        }

        // Verifica IDs que sabidamente requerem reboot
        private bool IsRebootRequired(string tweakId)
        {
            var rebootIds = new HashSet<string> 
            { 
                "PF4", // SysMain
                "PF7", // GPU Scheduling
                "PF8", // VBS/HVCI
                "A1",  // Transparência (as vezes requer relogin)
                "P1",  // Telemetria (políticas)
            };
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
                _ => Enumerable.Empty<TweakViewModel>()
            };

            int successCount = 0;
            int failCount = 0;
            string lastError = "";
            bool rebootNeeded = false;

            await Task.Run(() =>
            {
                foreach (var item in list.Where(x => x.IsSelected))
                {
                    if (IsRebootRequired(item.Id)) rebootNeeded = true;

                    var result = item.Tweak.Apply();
                    if (result.Success) successCount++;
                    else 
                    {
                        failCount++;
                        lastError = result.Message;
                    }
                }
            });

            foreach (var item in list)
            {
                item.IsSelected = false;
                item.UpdateStatusUI();
            }

            IsBusy = false;

            if (failCount > 0)
            {
                await _dialogService.ShowMessageAsync("Resultado da Aplicação", 
                    $"Concluído com erros.\nSucessos: {successCount}\nFalhas: {failCount}\nÚltimo erro: {lastError}");
            }
            else if (successCount > 0)
            {
                if (rebootNeeded)
                {
                    await _dialogService.ShowMessageAsync("Sucesso - Reinicialização Necessária", 
                        "Todas as otimizações foram aplicadas.\n\nATENÇÃO: Algumas alterações (como VBS, GPU ou Serviços) requerem que você REINICIE o computador para surtir efeito completo.");
                }
                else
                {
                    await _dialogService.ShowMessageAsync("Sucesso", "Todas as otimizações selecionadas foram aplicadas com sucesso.");
                }
            }
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
                _ => Enumerable.Empty<TweakViewModel>()
            };

            int successCount = 0;
            int failCount = 0;
            string lastError = "";
            bool rebootNeeded = false;

            await Task.Run(() =>
            {
                foreach (var item in list.Where(x => x.IsSelected))
                {
                    if (IsRebootRequired(item.Id)) rebootNeeded = true;

                    var result = item.Tweak.Revert();
                    if (result.Success) successCount++;
                    else 
                    {
                        failCount++;
                        lastError = result.Message;
                    }
                }
            });

            foreach (var item in list)
            {
                item.IsSelected = false;
                item.UpdateStatusUI();
            }

            IsBusy = false;

            if (failCount > 0)
            {
                await _dialogService.ShowMessageAsync("Resultado da Restauração", 
                    $"Concluído com erros.\nSucessos: {successCount}\nFalhas: {failCount}\nÚltimo erro: {lastError}");
            }
            else if (successCount > 0)
            {
                if (rebootNeeded)
                {
                    await _dialogService.ShowMessageAsync("Sucesso - Reinicialização Necessária", 
                        "As configurações foram restauradas.\n\nPor favor, REINICIE o computador para garantir que as alterações no sistema sejam efetivadas.");
                }
                else
                {
                    await _dialogService.ShowMessageAsync("Sucesso", "Todas as otimizações selecionadas foram restauradas para o padrão.");
                }
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
}
