using System;
using System.Globalization;
using System.Windows.Data;

namespace Doc_Helper.UI.Converters
{
    /// <summary>
    /// Converts numeric progress values to percentage strings
    /// </summary>
    public class ProgressToPercentageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double progress)
            {
                return $"{progress:F1}%";
            }

            if (value is float progressFloat)
            {
                return $"{progressFloat:F1}%";
            }

            if (value is int progressInt)
            {
                return $"{progressInt}%";
            }

            return "0.0%";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string percentageString && percentageString.EndsWith("%"))
            {
                var numberPart = percentageString.Replace("%", "").Trim();
                if (double.TryParse(numberPart, out double result))
                {
                    return result;
                }
            }
            return 0.0;
        }
    }
}