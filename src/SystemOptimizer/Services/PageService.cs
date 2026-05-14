using System;
using Microsoft.Extensions.DependencyInjection;

namespace SystemOptimizer.Services;

public sealed class PageService
{
    private readonly IServiceProvider _serviceProvider;
    public PageService(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;
    public object GetPage(Type pageType) => _serviceProvider.GetRequiredService(pageType);
}
