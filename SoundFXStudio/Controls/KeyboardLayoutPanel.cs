using SoundFXStudio.Models;
using System.Windows;
using System.Windows.Controls;

namespace SoundFXStudio.Controls;

public sealed class KeyboardLayoutPanel : Panel
{
    private const double KeyUnit = 54;
    private const double Gap = 6;

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        }

        // Calculate the desired size based on content layout
        var maxWidth = InternalChildren.OfType<FrameworkElement>()
            .Select(child => child.DataContext is KeyboardKey key ? key.ColumnIndex + key.WidthUnits : 0d)
            .DefaultIfEmpty(0d)
            .Max();

        var maxHeight = InternalChildren.OfType<FrameworkElement>()
            .Select(child => child.DataContext is KeyboardKey key ? key.RowIndex + key.HeightUnits : 0d)
            .DefaultIfEmpty(0d)
            .Max();

        var desiredWidth = (maxWidth * (KeyUnit + Gap)) + Gap;
        var desiredHeight = (maxHeight * (KeyUnit + Gap)) + Gap;

        // Constrain to available size if not infinite
        return new Size(
            double.IsPositiveInfinity(availableSize.Width) ? desiredWidth : Math.Min(desiredWidth, availableSize.Width),
            double.IsPositiveInfinity(availableSize.Height) ? desiredHeight : Math.Min(desiredHeight, availableSize.Height)
        );
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (UIElement child in InternalChildren)
        {
            if (child is not FrameworkElement element || element.DataContext is not KeyboardKey key)
            {
                continue;
            }

            var width = (key.WidthUnits * KeyUnit) + ((key.WidthUnits - 1) * Gap);
            var height = (key.HeightUnits * KeyUnit) + ((key.HeightUnits - 1) * Gap);

            var x = key.ColumnIndex * (KeyUnit + Gap);
            var y = key.RowIndex * (KeyUnit + Gap);

            child.Arrange(new Rect(new Point(x, y), new Size(width, height)));
        }

        var maxWidth = InternalChildren.OfType<FrameworkElement>()
            .Select(child => child.DataContext is KeyboardKey key ? key.ColumnIndex + key.WidthUnits : 0d)
            .DefaultIfEmpty(0d)
            .Max();

        var maxHeight = InternalChildren.OfType<FrameworkElement>()
            .Select(child => child.DataContext is KeyboardKey key ? key.RowIndex + key.HeightUnits : 0d)
            .DefaultIfEmpty(0d)
            .Max();

        return new Size((maxWidth * (KeyUnit + Gap)) + Gap, (maxHeight * (KeyUnit + Gap)) + Gap);
    }
}