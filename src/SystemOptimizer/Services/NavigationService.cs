using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace SystemOptimizer.Services;

public sealed class NavigationService(IServiceProvider serviceProvider)
{
    private Frame? _frame;

    public void Initialize(Frame frame) => _frame = frame;

    public bool Navigate(Type pageType)
    {
        if (_frame is null) return false;
        var page = serviceProvider.GetRequiredService(pageType);
        _frame.Content = page;
        return true;
    }
}
