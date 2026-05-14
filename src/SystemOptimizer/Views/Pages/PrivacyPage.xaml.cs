using Microsoft.Extensions.DependencyInjection;
using SystemOptimizer.Properties;
namespace SystemOptimizer.Views.Pages;
public sealed class PrivacyPage : TweakCategoryPage { public PrivacyPage() : base(Resources.Nav_Privacy, "Privacy", App.Services.GetRequiredService<SystemOptimizer.ViewModels.MainViewModel>().PrivacyTweaks) { } }
