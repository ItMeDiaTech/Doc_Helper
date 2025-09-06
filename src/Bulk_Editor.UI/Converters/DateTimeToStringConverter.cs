using System;
using System.Globalization;
using System.Windows.Data;

namespace Doc_Helper.UI.Converters
{
    /// <summary>
    /// Converts DateTime values to formatted strings
    /// </summary>
    public class DateTimeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dateTime)
            {
                // Use parameter as format string if provided, otherwise use default
                string format = parameter?.ToString() ?? "yyyy-MM-dd HH:mm:ss";

                // Handle special format keywords
                format = format.ToLower() switch
                {
                    "short" => "HH:mm:ss",
                    "date" => "yyyy-MM-dd",
                    "time" => "HH:mm:ss",
                    "datetime" => "yyyy-MM-dd HH:mm:ss",
                    "friendly" => "MMM dd, yyyy HH:mm",
                    "relative" => GetRelativeTime(dateTime),
                    _ => format
                };

                return format == "relative" ? format : dateTime.ToString(format, culture);
            }

            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string dateString && DateTime.TryParse(dateString, culture, DateTimeStyles.None, out DateTime result))
            {
                return result;
            }
            return DateTime.MinValue;
        }

        private static string GetRelativeTime(DateTime dateTime)
        {
            var timeSpan = DateTime.Now - dateTime;

            return timeSpan.TotalSeconds switch
            {
                < 60 => "Just now",
                < 3600 => $"{(int)timeSpan.TotalMinutes} minutes ago",
                < 86400 => $"{(int)timeSpan.TotalHours} hours ago",
                < 604800 => $"{(int)timeSpan.TotalDays} days ago",
                _ => dateTime.ToString("MMM dd, yyyy")
            };
        }
    }
}