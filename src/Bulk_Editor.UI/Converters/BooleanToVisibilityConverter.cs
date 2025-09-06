using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Doc_Helper.UI.Converters
{
    /// <summary>
    /// Converts boolean values to Visibility enum values
    /// True = Visible, False = Collapsed
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                // Check if parameter requests inverse behavior
                bool inverse = parameter?.ToString()?.ToLower() == "inverse";
                return inverse
                    ? (boolValue ? Visibility.Collapsed : Visibility.Visible)
                    : (boolValue ? Visibility.Visible : Visibility.Collapsed);
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                bool inverse = parameter?.ToString()?.ToLower() == "inverse";
                return inverse
                    ? visibility != Visibility.Visible
                    : visibility == Visibility.Visible;
            }
            return false;
        }
    }
}