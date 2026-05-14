using Microsoft.Extensions.DependencyInjection;
using SystemOptimizer.Properties;
namespace SystemOptimizer.Views.Pages;
public sealed class SecurityPage : TweakCategoryPage { public SecurityPage() : base(Resources.Nav_Security, "Security", App.Services.GetRequiredService<SystemOptimizer.ViewModels.MainViewModel>().SecurityTweaks) { } }
