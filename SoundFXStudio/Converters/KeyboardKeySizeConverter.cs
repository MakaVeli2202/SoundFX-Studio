using System.Globalization;
using System.Windows.Data;

namespace SoundFXStudio.Converters;

public sealed class KeyboardKeySizeConverter : IValueConverter
{
    private const double Unit = 54;
    private const double Gap = 6;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var units = value switch
        {
            double doubleValue => doubleValue,
            float floatValue => floatValue,
            int intValue => intValue,
            _ => 1d
        };

        var result = units * Unit + ((units - 1) * Gap);
        return result;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}