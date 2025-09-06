using System;
using System.Globalization;
using System.Windows.Data;

namespace Doc_Helper.UI.Converters
{
    /// <summary>
    /// Converts file count numbers to descriptive text
    /// </summary>
    public class FileCountToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                return count switch
                {
                    0 => "No files selected",
                    1 => "1 file selected",
                    _ => $"{count} files selected"
                };
            }

            // Handle collections
            if (value is System.Collections.ICollection collection)
            {
                return collection.Count switch
                {
                    0 => "No files selected",
                    1 => "1 file selected",
                    _ => $"{collection.Count} files selected"
                };
            }

            return "No files selected";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("FileCountToStringConverter does not support ConvertBack");
        }
    }
}