using System.Globalization;
using System.Windows.Data;

namespace SoundFXStudio.Converters;

public sealed class LedReflectionWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 11)
        {
            return 0d;
        }

        var width = ToDouble(values[0]);
        var globalInsetXPercent = ToDouble(values[2]);
        var globalOffsetXPercent = ToDouble(values[4]);
        var keyInsetAdjustment = ToDouble(values[6]);
        var keyInsetXAdjustment = ToDouble(values[7]);
        var keyOffsetXAdjustment = ToDouble(values[9]);

        var insetXPercent = Math.Clamp(globalInsetXPercent + keyInsetAdjustment + keyInsetXAdjustment, 0d, 45d);
        var offsetXPercent = Math.Clamp(globalOffsetXPercent + keyOffsetXAdjustment, -30d, 30d);

        var insetX = width * (insetXPercent / 100d);
        var offsetX = width * (offsetXPercent / 100d);

        var isLeft = string.Equals(parameter as string, "Left", StringComparison.OrdinalIgnoreCase);
        return Math.Max(0d, isLeft ? insetX + offsetX : insetX - offsetX);
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
