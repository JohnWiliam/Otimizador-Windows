using System;
using System.Globalization;
using Microsoft.UI.Xaml.Data;

namespace SystemOptimizer.Helpers;

public class MathConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, string language)
    {
        if (value is double dValue &&
            double.TryParse(parameter?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double dParam))
        {
            return dValue + dParam;
        }
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, string language)
    {
        if (!double.TryParse(value?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double dValue)) return value;
        if (double.TryParse(parameter?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double dParam))
        {
            return dValue - dParam;
        }
        return value;
    }
}
