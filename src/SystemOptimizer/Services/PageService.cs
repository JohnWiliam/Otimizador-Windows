using System;
using System.Windows;
using Wpf.Ui;
using Wpf.Ui.Abstractions;

namespace SystemOptimizer.Services;

// Adicionamos "IPageService" aqui na lista de interfaces
public class PageService(IServiceProvider serviceProvider) : IPageService, INavigationViewPageProvider
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public T? GetPage<T>() where T : class
    {
        if (!typeof(FrameworkElement).IsAssignableFrom(typeof(T)))
            throw new InvalidOperationException("The page should be a WPF control.");

        return (T?)_serviceProvider.GetService(typeof(T));
    }

    public object? GetPage(Type pageType)
    {
        if (!typeof(FrameworkElement).IsAssignableFrom(pageType))
            throw new InvalidOperationException("The page should be a WPF control.");

        return _serviceProvider.GetService(pageType);
    }
}
