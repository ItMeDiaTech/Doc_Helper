using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Doc_Helper.UI.Converters
{
    /// <summary>
    /// Converts count values to Visibility
    /// Count > 0 = Visible, Count = 0 = Collapsed
    /// </summary>
    public class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int count = 0;

            if (value is int intValue)
            {
                count = intValue;
            }
            else if (value is System.Collections.ICollection collection)
            {
                count = collection.Count;
            }
            else if (value is System.Collections.IEnumerable enumerable)
            {
                count = 0;
                foreach (var item in enumerable)
                {
                    count++;
                }
            }

            // Check if parameter requests inverse behavior
            bool inverse = parameter?.ToString()?.ToLower() == "inverse";

            return inverse
                ? (count > 0 ? Visibility.Collapsed : Visibility.Visible)
                : (count > 0 ? Visibility.Visible : Visibility.Collapsed);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("CountToVisibilityConverter does not support ConvertBack");
        }
    }
}