using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace SystemOptimizer.Services;

public sealed class PageService(IServiceProvider serviceProvider)
{
    public T GetPage<T>() where T : FrameworkElement => serviceProvider.GetRequiredService<T>();
    public object GetPage(Type pageType) => serviceProvider.GetRequiredService(pageType);
}
