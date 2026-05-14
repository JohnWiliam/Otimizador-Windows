using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace SystemOptimizer.Helpers;

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value is bool b && !b;
    public object ConvertBack(object value, Type targetType, object parameter, string language) => value is bool b && !b;
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value is bool b && b ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, string language) => value is Visibility v && v == Visibility.Visible;
}

public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value is bool b && b ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type targetType, object parameter, string language) => value is Visibility v && v == Visibility.Collapsed;
}
