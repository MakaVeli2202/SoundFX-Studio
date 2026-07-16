using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SoundFXStudio.Converters;

public sealed class InnerSectionMarginConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 11)
        {
            return new Thickness(0);
        }

        var width = ToDouble(values[0]);
        var height = ToDouble(values[1]);
        var globalInsetXPercent = ToDouble(values[2]);
        var globalInsetYPercent = ToDouble(values[3]);
        var globalOffsetXPercent = ToDouble(values[4]);
        var globalOffsetYPercent = ToDouble(values[5]);
        var keyInsetAdjustment = ToDouble(values[6]);
        var keyInsetXAdjustment = ToDouble(values[7]);
        var keyInsetYAdjustment = ToDouble(values[8]);
        var keyOffsetXAdjustment = ToDouble(values[9]);
        var keyOffsetYAdjustment = ToDouble(values[10]);

        var insetXPercent = Math.Clamp(globalInsetXPercent + keyInsetAdjustment + keyInsetXAdjustment, 0d, 45d);
        var insetYPercent = Math.Clamp(globalInsetYPercent + keyInsetAdjustment + keyInsetYAdjustment, 0d, 45d);
        var offsetXPercent = Math.Clamp(globalOffsetXPercent + keyOffsetXAdjustment, -30d, 30d);
        var offsetYPercent = Math.Clamp(globalOffsetYPercent + keyOffsetYAdjustment, -30d, 30d);

        var insetX = width * (insetXPercent / 100d);
        var insetY = height * (insetYPercent / 100d);

        var offsetX = width * (offsetXPercent / 100d);
        var offsetY = height * (offsetYPercent / 100d);

        var left = Math.Max(0d, insetX + offsetX);
        var right = Math.Max(0d, insetX - offsetX);
        var top = Math.Max(0d, insetY + offsetY);
        var bottom = Math.Max(0d, insetY - offsetY);

        return new Thickness(left, top, right, bottom);
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
