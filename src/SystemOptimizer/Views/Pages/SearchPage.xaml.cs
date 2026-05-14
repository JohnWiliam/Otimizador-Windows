using Microsoft.Extensions.DependencyInjection;
using SystemOptimizer.Properties;
namespace SystemOptimizer.Views.Pages;
public sealed class SearchPage : TweakCategoryPage { public SearchPage() : base(Resources.Nav_Search, "Search", App.Services.GetRequiredService<SystemOptimizer.ViewModels.MainViewModel>().SearchTweaks) { } }
