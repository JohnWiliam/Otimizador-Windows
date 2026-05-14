using Microsoft.Extensions.DependencyInjection;
using SystemOptimizer.Properties;
namespace SystemOptimizer.Views.Pages;
public sealed class NetworkPage : TweakCategoryPage { public NetworkPage() : base(Resources.Nav_Network, "Network", App.Services.GetRequiredService<SystemOptimizer.ViewModels.MainViewModel>().NetworkTweaks) { } }
