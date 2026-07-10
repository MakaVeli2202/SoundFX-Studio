using System.Globalization;
using System.Windows.Data;

namespace SoundFXStudio.Converters;

public sealed class KeyboardKeySizeConverter : IValueConverter, IMultiValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var units = value switch
        {
            double doubleValue => doubleValue,
            float floatValue => floatValue,
            int intValue => intValue,
            _ => 1d
        };

        var result = GetScaledSize(units, 0.8d);
        return result;
    }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var units = values.Length > 0
            ? values[0] switch
            {
                double doubleValue => doubleValue,
                float floatValue => floatValue,
                int intValue => intValue,
                _ => 1d
            }
            : 1d;

        var scale = values.Length > 1 && values[1] is double doubleScale ? doubleScale : 0.8d;
        return GetScaledSize(units, scale);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static double GetScaledSize(double units, double scale)
    {
        var unit = SoundFXStudio.Controls.KeyboardLayoutPanel.KeyUnit;
        var gap = SoundFXStudio.Controls.KeyboardLayoutPanel.Gap;
        return (units * unit + ((units - 1) * gap)) * scale;
    }
}