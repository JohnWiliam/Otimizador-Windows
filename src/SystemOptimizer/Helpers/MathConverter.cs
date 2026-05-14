using System;
using System.Globalization;
using Microsoft.UI.Xaml.Data;

namespace SystemOptimizer.Helpers;

public sealed class MathConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, string language)
    {
        if (value is double dValue && double.TryParse(parameter?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var dParam))
        {
            return dValue + dParam;
        }

        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, string language)
    {
        if (!double.TryParse(value?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var dValue))
        {
            return value;
        }

        return double.TryParse(parameter?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var dParam)
            ? dValue - dParam
            : value;
    }
}
