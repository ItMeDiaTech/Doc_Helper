using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Doc_Helper.UI.Converters
{
    /// <summary>
    /// Converts processing status strings to appropriate colors
    /// </summary>
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                return status.ToLower() switch
                {
                    "processing" => Brushes.Orange,
                    "complete" => Brushes.Green,
                    "success" => Brushes.Green,
                    "completed" => Brushes.Green,
                    "error" => Brushes.Red,
                    "failed" => Brushes.Red,
                    "failure" => Brushes.Red,
                    "cancelled" => Brushes.Gray,
                    "canceled" => Brushes.Gray,
                    "idle" => Brushes.Gray,
                    "ready" => Brushes.Blue,
                    "warning" => Brushes.Goldenrod,
                    _ => Brushes.Black
                };
            }
            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("StatusToColorConverter does not support ConvertBack");
        }
    }
}