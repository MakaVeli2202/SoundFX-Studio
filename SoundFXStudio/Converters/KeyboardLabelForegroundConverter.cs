using SoundFXStudio.Models;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SoundFXStudio.Converters;

public sealed class KeyboardLabelForegroundConverter : IMultiValueConverter
{
    private static readonly Brush DefaultBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
    private static readonly Brush FallbackPressedBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22D3FF"));

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var state = values.Length > 0 && values[0] is KeyState keyState
            ? keyState
            : KeyState.Empty;

        if (state != KeyState.Pressed)
        {
            return DefaultBrush;
        }

        if (values.Length > 1 && values[1] is string colorText && !string.IsNullOrWhiteSpace(colorText))
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(colorText.Trim());
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
            catch
            {
                return FallbackPressedBrush;
            }
        }

        return FallbackPressedBrush;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}