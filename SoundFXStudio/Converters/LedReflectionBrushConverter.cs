using SoundFXStudio.Models;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SoundFXStudio.Converters;

public sealed class LedReflectionBrushConverter : IMultiValueConverter
{
    private const byte BottomAlpha = 150;

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var color = ResolveColor(values);

        var brush = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 1),
            EndPoint = new System.Windows.Point(0, 0)
        };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(BottomAlpha, color.R, color.G, color.B), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 0.5));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1));
        brush.Freeze();
        return brush;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static Color ResolveColor(object[] values)
    {
        if (values.Length > 0 && values[0] is KeyState state && state is KeyState.Pressed or KeyState.Playing)
        {
            return values.Length > 1 && values[1] is string text && !string.IsNullOrWhiteSpace(text)
                ? TryParse(text)
                : Colors.Transparent;
        }

        return Colors.Transparent;
    }

    private static Color TryParse(string text)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(text.Trim());
        }
        catch
        {
            return Colors.Transparent;
        }
    }
}
