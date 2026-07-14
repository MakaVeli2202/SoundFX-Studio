using System.Globalization;
using System.Windows.Data;
using SoundFXStudio.Models;

namespace SoundFXStudio.Converters;

public sealed class KeyboardLayoutDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value switch
        {
            KeyboardLayoutMode.Automatic => "Automatic (Default)",
            KeyboardLayoutMode.EnglishUK => "English UK",
            KeyboardLayoutMode.EnglishUS => "English US",
            KeyboardLayoutMode.German => "German",
            _ => value?.ToString() ?? string.Empty
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
