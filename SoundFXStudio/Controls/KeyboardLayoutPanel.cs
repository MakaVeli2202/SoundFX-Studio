using SoundFXStudio.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SoundFXStudio.Controls;

public sealed class KeyboardLayoutPanel : Panel
{
    private const double BaseLayoutWidth = 1536;
    private const double BaseLayoutHeight = 1024;

    private static double _keyUnit = 43;
    private static double _gapX = 3;
    private static double _gapY = 3;
    private static double _offsetX = 65;
    private static double _offsetY = 72;
    private static double _buttonScale = 1.0;

    private static readonly Dictionary<string, PerKeyOverride> PerKeyOverrides = new(StringComparer.OrdinalIgnoreCase);
    private static event Action? CalibrationChanged;

    public static bool DebugKeyboardCalibration { get; set; }

    public static double KeyUnit
    {
        get => _keyUnit;
        set
        {
            if (Math.Abs(_keyUnit - value) < double.Epsilon)
            {
                return;
            }

            _keyUnit = value;
            NotifyCalibrationChanged();
        }
    }

    // Backward-compatible alias used by existing code paths.
    public static double Gap
    {
        get => _gapX;
        set
        {
            GapX = value;
            GapY = value;
        }
    }

    public static double GapX
    {
        get => _gapX;
        set
        {
            if (Math.Abs(_gapX - value) < double.Epsilon)
            {
                return;
            }

            _gapX = value;
            NotifyCalibrationChanged();
        }
    }

    public static double GapY
    {
        get => _gapY;
        set
        {
            if (Math.Abs(_gapY - value) < double.Epsilon)
            {
                return;
            }

            _gapY = value;
            NotifyCalibrationChanged();
        }
    }

    public static double OffsetX
    {
        get => _offsetX;
        set
        {
            if (Math.Abs(_offsetX - value) < double.Epsilon)
            {
                return;
            }

            _offsetX = value;
            NotifyCalibrationChanged();
        }
    }

    public static double OffsetY
    {
        get => _offsetY;
        set
        {
            if (Math.Abs(_offsetY - value) < double.Epsilon)
            {
                return;
            }

            _offsetY = value;
            NotifyCalibrationChanged();
        }
    }

    public static double ButtonScale
    {
        get => _buttonScale;
        set
        {
            if (Math.Abs(_buttonScale - value) < double.Epsilon)
            {
                return;
            }

            _buttonScale = value;
            NotifyCalibrationChanged();
        }
    }

    public static void SetLayoutCalibration(double keyUnit, double gapX, double gapY, double offsetX, double offsetY)
    {
        _keyUnit = keyUnit;
        _gapX = gapX;
        _gapY = gapY;
        _offsetX = offsetX;
        _offsetY = offsetY;
        NotifyCalibrationChanged();
    }

    public static void SetPerKeyOverride(string keyId, double offsetX, double offsetY, double widthAdjustment, double heightAdjustment)
    {
        PerKeyOverrides[keyId] = new PerKeyOverride(offsetX, offsetY, widthAdjustment, heightAdjustment);
        NotifyCalibrationChanged();
    }

    public static void ClearPerKeyOverride(string keyId)
    {
        PerKeyOverrides.Remove(keyId);
        NotifyCalibrationChanged();
    }

    public static void ClearAllPerKeyOverrides()
    {
        PerKeyOverrides.Clear();
        NotifyCalibrationChanged();
    }

    public KeyboardLayoutPanel()
    {
        Loaded += KeyboardLayoutPanel_Loaded;
        Unloaded += KeyboardLayoutPanel_Unloaded;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        }

        var maxWidth = InternalChildren.OfType<FrameworkElement>()
            .Select(child => child.DataContext is KeyboardKey key ? GetKeyRightEdge(key) : 0d)
            .DefaultIfEmpty(0d)
            .Max();

        var maxHeight = InternalChildren.OfType<FrameworkElement>()
            .Select(child => child.DataContext is KeyboardKey key ? GetKeyBottomEdge(key) : 0d)
            .DefaultIfEmpty(0d)
            .Max();

        var desiredWidth = Math.Max(BaseLayoutWidth, Math.Max(0d, maxWidth + GapX));
        var desiredHeight = Math.Max(BaseLayoutHeight, Math.Max(0d, maxHeight + GapY));

        return new Size(
            double.IsPositiveInfinity(availableSize.Width) ? desiredWidth : Math.Min(desiredWidth, availableSize.Width),
            double.IsPositiveInfinity(availableSize.Height) ? desiredHeight : Math.Min(desiredHeight, availableSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (UIElement child in InternalChildren)
        {
            if (child is not FrameworkElement element || element.DataContext is not KeyboardKey key)
            {
                continue;
            }

            if (DebugKeyboardCalibration && child is Button button)
            {
                button.Background = new SolidColorBrush(Color.FromArgb(40, 255, 0, 0));
                button.BorderBrush = Brushes.Red;
                button.BorderThickness = new Thickness(1);
                button.Opacity = 1;
            }

            var keyOverride = GetPerKeyOverride(key);
            var baseWidth = Math.Max(1d, (key.WidthUnits * KeyUnit) + keyOverride.WidthAdjustment);
            var baseHeight = Math.Max(1d, (key.HeightUnits * KeyUnit) + keyOverride.HeightAdjustment);
            var width = Math.Max(1d, baseWidth * ButtonScale);
            var height = Math.Max(1d, baseHeight * ButtonScale);

            // Keep scaled keys centered within their logical slot so global spacing stays stable.
            var x = OffsetX + key.ColumnIndex * (KeyUnit + GapX) + keyOverride.OffsetX + ((baseWidth - width) / 2d);
            var y = OffsetY + key.RowIndex * (KeyUnit + GapY) + keyOverride.OffsetY + ((baseHeight - height) / 2d);

            child.Arrange(new Rect(new Point(x, y), new Size(width, height)));
        }

        var maxWidth = InternalChildren.OfType<FrameworkElement>()
            .Select(child => child.DataContext is KeyboardKey key ? GetKeyRightEdge(key) : 0d)
            .DefaultIfEmpty(0d)
            .Max();

        var maxHeight = InternalChildren.OfType<FrameworkElement>()
            .Select(child => child.DataContext is KeyboardKey key ? GetKeyBottomEdge(key) : 0d)
            .DefaultIfEmpty(0d)
            .Max();

        return new Size(
            Math.Max(BaseLayoutWidth, Math.Max(0d, maxWidth + GapX)),
            Math.Max(BaseLayoutHeight, Math.Max(0d, maxHeight + GapY)));
    }

    private static PerKeyOverride GetPerKeyOverride(KeyboardKey key)
        => string.IsNullOrWhiteSpace(key.Id) || !PerKeyOverrides.TryGetValue(key.Id, out var calibration)
            ? default
            : calibration;

    private static void NotifyCalibrationChanged()
    {
        CalibrationChanged?.Invoke();
    }

    private void KeyboardLayoutPanel_Loaded(object sender, RoutedEventArgs e)
    {
        CalibrationChanged += HandleCalibrationChanged;
    }

    private void KeyboardLayoutPanel_Unloaded(object sender, RoutedEventArgs e)
    {
        CalibrationChanged -= HandleCalibrationChanged;
    }

    private void HandleCalibrationChanged()
    {
        InvalidateMeasure();
        InvalidateArrange();
        UpdateLayout();
    }

    private static double GetKeyRightEdge(KeyboardKey key)
    {
        var keyOverride = GetPerKeyOverride(key);
        var baseWidth = Math.Max(1d, (key.WidthUnits * KeyUnit) + keyOverride.WidthAdjustment);
        var width = Math.Max(1d, baseWidth * ButtonScale);
        var x = OffsetX + key.ColumnIndex * (KeyUnit + GapX) + keyOverride.OffsetX + ((baseWidth - width) / 2d);
        return x + width;
    }

    private static double GetKeyBottomEdge(KeyboardKey key)
    {
        var keyOverride = GetPerKeyOverride(key);
        var baseHeight = Math.Max(1d, (key.HeightUnits * KeyUnit) + keyOverride.HeightAdjustment);
        var height = Math.Max(1d, baseHeight * ButtonScale);
        var y = OffsetY + key.RowIndex * (KeyUnit + GapY) + keyOverride.OffsetY + ((baseHeight - height) / 2d);
        return y + height;
    }

    private readonly record struct PerKeyOverride(double OffsetX, double OffsetY, double WidthAdjustment, double HeightAdjustment);
}
