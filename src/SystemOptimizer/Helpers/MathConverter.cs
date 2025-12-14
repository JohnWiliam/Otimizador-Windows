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
            throw new NotImplementedException();
        }
    }
}
