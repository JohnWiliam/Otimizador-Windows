using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SystemOptimizer.Helpers;
using SystemOptimizer.Views.Pages;

namespace SystemOptimizer.Services;

public interface INavigationCoordinator
{
    void Initialize(Frame frame);
    bool Navigate(Type pageType);
    bool NavigateToSettings();
}

public sealed class NavigationCoordinator : INavigationCoordinator
{
    private Frame? _frame;

    public void Initialize(Frame frame) => _frame = frame;

    public bool Navigate(Type pageType)
    {
        if (_frame is null) return false;

        if (_frame.DispatcherQueue.HasThreadAccess)
        {
            return _frame.Navigate(pageType);
        }

        _frame.DispatcherQueue.TryEnqueue(() => _frame.Navigate(pageType));
        return true;
    }

    public bool NavigateToSettings() => Navigate(typeof(SettingsPage));
}

public interface IXamlRootProvider
{
    XamlRoot? XamlRoot { get; }
    void Initialize(FrameworkElement rootElement);
    void Clear();
}

public sealed class XamlRootProvider : IXamlRootProvider
{
    private WeakReference<FrameworkElement>? _rootElement;

    public XamlRoot? XamlRoot
        => _rootElement != null && _rootElement.TryGetTarget(out var element) ? element.XamlRoot : null;

    public void Initialize(FrameworkElement rootElement)
        => _rootElement = new WeakReference<FrameworkElement>(rootElement);

    public void Clear() => _rootElement = null;
}
