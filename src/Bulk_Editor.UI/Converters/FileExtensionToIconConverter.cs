using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace Doc_Helper.UI.Converters
{
    /// <summary>
    /// Converts file extensions to appropriate icon representations
    /// </summary>
    public class FileExtensionToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string extension = string.Empty;

            if (value is string filePath)
            {
                extension = Path.GetExtension(filePath);
            }
            else if (value is FileInfo fileInfo)
            {
                extension = fileInfo.Extension;
            }

            return extension.ToLower() switch
            {
                ".docx" => "📄",
                ".doc" => "📄",
                ".xlsx" => "📊",
                ".xls" => "📊",
                ".pptx" => "📊",
                ".ppt" => "📊",
                ".pdf" => "📕",
                ".txt" => "📝",
                ".rtf" => "📝",
                ".xml" => "📄",
                ".json" => "📄",
                ".csv" => "📊",
                ".zip" => "📦",
                ".rar" => "📦",
                ".7z" => "📦",
                ".exe" => "⚙️",
                ".dll" => "⚙️",
                ".html" => "🌐",
                ".htm" => "🌐",
                ".jpg" => "🖼️",
                ".jpeg" => "🖼️",
                ".png" => "🖼️",
                ".gif" => "🖼️",
                ".bmp" => "🖼️",
                ".mp4" => "🎬",
                ".avi" => "🎬",
                ".mov" => "🎬",
                ".mp3" => "🎵",
                ".wav" => "🎵",
                _ => "📁"
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("FileExtensionToIconConverter does not support ConvertBack");
        }
    }
}