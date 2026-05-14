using Microsoft.Extensions.DependencyInjection;
using SystemOptimizer.Properties;
namespace SystemOptimizer.Views.Pages;
public sealed class PerformancePage : TweakCategoryPage { public PerformancePage() : base(Resources.Nav_Performance, "Performance", App.Services.GetRequiredService<SystemOptimizer.ViewModels.MainViewModel>().PerformanceTweaks) { } }
