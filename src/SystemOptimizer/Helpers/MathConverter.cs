using System;
using System.Globalization;
using System.Windows.Data;

namespace SystemOptimizer.Helpers
{
    public class MathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double dValue)
            {
                // Parse parameter using InvariantCulture to ensure '.' is treated as decimal separator if used,
                // though usually we pass integers like "-100".
                if (double.TryParse(parameter?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double dParam))
                {
                    return dValue + dParam;
                }
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double dValue;
            bool isDouble = value is double;

            if (isDouble)
            {
                dValue = (double)value;
            }
            else if (!double.TryParse(value?.ToString(), NumberStyles.Any, culture, out dValue) &&
                     !double.TryParse(value?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out dValue))
            {
                // If it's not a double and we can't parse it with either culture, return the original value.
                return value;
            }

            // Parse parameter using InvariantCulture consistent with Convert method
            if (double.TryParse(parameter?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double dParam))
            {
                return dValue - dParam;
            }

            return value;
        }
    }
}
