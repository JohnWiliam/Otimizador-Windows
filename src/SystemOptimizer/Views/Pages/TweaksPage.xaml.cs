using Microsoft.Extensions.DependencyInjection;
using SystemOptimizer.Properties;
namespace SystemOptimizer.Views.Pages;
public sealed class TweaksPage : TweakCategoryPage { public TweaksPage() : base(Resources.Nav_Tweaks, "Tweaks", App.Services.GetRequiredService<SystemOptimizer.ViewModels.MainViewModel>().TweaksPageItems) { } }
