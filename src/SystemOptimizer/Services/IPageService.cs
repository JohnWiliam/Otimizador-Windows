using System;
using System.Windows;

namespace SystemOptimizer.Services;

public interface IPageService
{
    T? GetPage<T>() where T : class;
    object? GetPage(Type pageType);
}
