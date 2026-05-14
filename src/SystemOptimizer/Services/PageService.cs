using System;

namespace SystemOptimizer.Services;

public sealed class PageService(IServiceProvider serviceProvider)
{
    public object? GetPage(Type pageType) => serviceProvider.GetService(pageType);
}
