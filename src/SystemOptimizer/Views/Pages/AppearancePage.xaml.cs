using Microsoft.Extensions.DependencyInjection;
using SystemOptimizer.Properties;
namespace SystemOptimizer.Views.Pages;
public sealed class AppearancePage : TweakCategoryPage { public AppearancePage() : base(Resources.Nav_Visual, "Appearance", App.Services.GetRequiredService<SystemOptimizer.ViewModels.MainViewModel>().AppearanceTweaks) { } }
