using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Doc_Helper.UI.Converters
{
    public class StatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                return status.ToLowerInvariant() switch
                {
                    "success" => new SolidColorBrush(Color.FromRgb(76, 175, 80)),   // Green
                    "failed" or "error" => new SolidColorBrush(Color.FromRgb(244, 67, 54)),    // Red
                    "warning" => new SolidColorBrush(Color.FromRgb(255, 152, 0)),   // Orange
                    "processing" or "in progress" => new SolidColorBrush(Color.FromRgb(33, 150, 243)), // Blue
                    _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))          // Gray
                };
            }
            
            return new SolidColorBrush(Color.FromRgb(158, 158, 158));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    public class LogLevelToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string level)
            {
                return level.ToUpperInvariant() switch
                {
                    "INFO" or "INFORMATION" => new SolidColorBrush(Color.FromRgb(33, 150, 243)),  // Blue
                    "WARN" or "WARNING" => new SolidColorBrush(Color.FromRgb(255, 152, 0)),       // Orange
                    "ERROR" => new SolidColorBrush(Color.FromRgb(244, 67, 54)),                   // Red
                    "DEBUG" => new SolidColorBrush(Color.FromRgb(156, 39, 176)),                  // Purple
                    _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))                        // Gray
                };
            }
            
            return new SolidColorBrush(Color.FromRgb(158, 158, 158));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    public class BooleanToProcessingColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isProcessing)
            {
                return isProcessing 
                    ? new SolidColorBrush(Color.FromRgb(76, 175, 80))    // Green when processing
                    : new SolidColorBrush(Color.FromRgb(158, 158, 158)); // Gray when idle
            }
            
            return new SolidColorBrush(Color.FromRgb(158, 158, 158));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}