using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using System.Windows.Media;
using SystemOptimizer.Services;
using SystemOptimizer.Properties;

namespace SystemOptimizer.ViewModels;

public partial class SearchFixViewModel : ObservableObject
{
    private readonly SearchRegistryService _service;

    // Colors
    private readonly SolidColorBrush _colorOptimized = new(Colors.LightGreen);
    private readonly SolidColorBrush _colorNotOptimized = new(Colors.Gray);

    // Observable Properties
    [ObservableProperty] private string _textSuggestions = Resources.Status_Loading;
    [ObservableProperty] private SolidColorBrush _colorSuggestions;

    [ObservableProperty] private string _textCloud = Resources.Status_Loading;
    [ObservableProperty] private SolidColorBrush _colorCloud;

    [ObservableProperty] private string _textBing = Resources.Status_Loading;
    [ObservableProperty] private SolidColorBrush _colorBing;

    public SearchFixViewModel(SearchRegistryService service)
    {
        _service = service;
        _colorSuggestions = _colorNotOptimized;
        _colorCloud = _colorNotOptimized;
        _colorBing = _colorNotOptimized;
        
        CheckAllStatuses();
    }

    private void CheckAllStatuses()
    {
        // 1. Suggestions
        var status1 = _service.CheckSearchBoxSuggestions();
        TextSuggestions = status1 == SearchRegistryService.OptimizationStatus.Optimized 
            ? Resources.SearchFix_Status_Optimized 
            : Resources.SearchFix_Status_Default;
        ColorSuggestions = status1 == SearchRegistryService.OptimizationStatus.Optimized ? _colorOptimized : _colorNotOptimized;

        // 2. Cloud
        var status2 = _service.CheckCloudSearch();
        TextCloud = status2 == SearchRegistryService.OptimizationStatus.Optimized 
            ? Resources.SearchFix_Status_Optimized 
            : Resources.SearchFix_Status_Default;
        ColorCloud = status2 == SearchRegistryService.OptimizationStatus.Optimized ? _colorOptimized : _colorNotOptimized;

        // 3. Bing
        var status3 = _service.CheckBingSearch();
        TextBing = status3 == SearchRegistryService.OptimizationStatus.Optimized 
            ? Resources.SearchFix_Status_Optimized 
            : Resources.SearchFix_Status_Default;
        ColorBing = status3 == SearchRegistryService.OptimizationStatus.Optimized ? _colorOptimized : _colorNotOptimized;
    }

    [RelayCommand]
    private void ApplySuggestions() { _service.ApplySearchBoxSuggestions(); CheckAllStatuses(); }
    [RelayCommand]
    private void RevertSuggestions() { _service.RevertSearchBoxSuggestions(); CheckAllStatuses(); }

    [RelayCommand]
    private void ApplyCloud() { _service.ApplyCloudSearch(); CheckAllStatuses(); }
    [RelayCommand]
    private void RevertCloud() { _service.RevertCloudSearch(); CheckAllStatuses(); }

    [RelayCommand]
    private void ApplyBing() { _service.ApplyBingSearch(); CheckAllStatuses(); }
    [RelayCommand]
    private void RevertBing() { _service.RevertBingSearch(); CheckAllStatuses(); }

    [RelayCommand]
    private async Task RestartExplorer()
    {
        await Task.Run(() => _service.RestartExplorer());
    }
}
