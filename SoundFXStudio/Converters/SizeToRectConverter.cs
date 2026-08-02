using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SoundFXStudio.Converters;

public sealed class SizeToRectConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var width = values.Length > 0 ? ToDouble(values[0]) : 0d;
        var height = values.Length > 1 ? ToDouble(values[1]) : 0d;
        return new Rect(0d, 0d, Math.Max(0d, width), Math.Max(0d, height));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static double ToDouble(object value)
    {
        return value switch
        {
            double d => d,
            float f => f,
            int i => i,
            _ => 0d
        };
    }
}
