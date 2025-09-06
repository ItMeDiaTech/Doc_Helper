using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Doc_Helper.UI.Converters
{
    /// <summary>
    /// Converts processing success/failure results to appropriate colors
    /// </summary>
    public class ProcessingResultToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool success)
            {
                return success ? Brushes.Green : Brushes.Red;
            }

            if (value is string result)
            {
                return result.ToLower() switch
                {
                    "success" => Brushes.Green,
                    "completed" => Brushes.Green,
                    "failed" => Brushes.Red,
                    "error" => Brushes.Red,
                    "warning" => Brushes.Orange,
                    "processing" => Brushes.Blue,
                    "pending" => Brushes.Gray,
                    _ => Brushes.Gray
                };
            }

            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ProcessingResultToColorConverter does not support ConvertBack");
        }
    }
}