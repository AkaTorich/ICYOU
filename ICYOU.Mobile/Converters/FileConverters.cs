using System.Globalization;
using Microsoft.Maui.Controls;

namespace ICYOU.Mobile.Converters;

public class FileTypeToIconConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length == 0 || values[0] is not string fileType)
            return "📄";

        return fileType.ToLowerInvariant() switch
        {
            "image" => "🖼️",
            "video" => "🎬",
            "audio" => "🎵",
            _ => "📄"
        };
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class FileTypeToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string fileType)
            return "Файл";

        return fileType.ToLowerInvariant() switch
        {
            "image" => "Изображение",
            "video" => "Видео",
            "audio" => "Аудио",
            _ => "Файл"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
