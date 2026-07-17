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

    private static readonly Dictionary<string, SpecialKeyOverride> SpecialKeyOverrides = new(StringComparer.OrdinalIgnoreCase);
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

    public static void SetSpecialKeyOverride(string keyId, double widthAdjustment)
    {
        SpecialKeyOverrides[keyId] = new SpecialKeyOverride(widthAdjustment);
        NotifyCalibrationChanged();
    }

    public static void ClearSpecialKeyOverride(string keyId)
    {
        SpecialKeyOverrides.Remove(keyId);
        NotifyCalibrationChanged();
    }

    public static void ClearAllSpecialKeyOverrides()
    {
        SpecialKeyOverrides.Clear();
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

            var clusterCalibration = KeyboardClusterLayout.Get(GetCluster(key));
            var specialOverride = GetSpecialOverride(key);
            var keyOverride = GetPerKeyOverride(key);
            var baseWidth = Math.Max(1d, (key.WidthUnits * KeyUnit) + specialOverride.WidthAdjustment + keyOverride.WidthAdjustment);
            var baseHeight = Math.Max(1d, (key.HeightUnits * KeyUnit) + keyOverride.HeightAdjustment);
            var width = Math.Max(1d, baseWidth * ButtonScale);
            var height = Math.Max(1d, baseHeight * ButtonScale);

            // Keep scaled keys centered within their logical slot so global spacing stays stable.
            var x = OffsetX + clusterCalibration.OffsetX + key.ColumnIndex * (KeyUnit + GapX) + keyOverride.OffsetX + ((baseWidth - width) / 2d);
            var y = OffsetY + clusterCalibration.OffsetY + key.RowIndex * (KeyUnit + GapY) + keyOverride.OffsetY + ((baseHeight - height) / 2d);

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

    private static SpecialKeyOverride GetSpecialOverride(KeyboardKey key)
    {
        var overrideKey = GetSpecialOverrideKey(key);
        return overrideKey is null || !SpecialKeyOverrides.TryGetValue(overrideKey, out var calibration)
            ? default
            : calibration;
    }

    private static string? GetSpecialOverrideKey(KeyboardKey key)
    {
        if (string.Equals(key.KeyName, "SPACE", StringComparison.OrdinalIgnoreCase))
        {
            return "SPACE";
        }

        if (string.Equals(key.KeyName, "BACKSPACE", StringComparison.OrdinalIgnoreCase))
        {
            return "BACKSPACE";
        }

        if (string.Equals(key.KeyName, "TAB", StringComparison.OrdinalIgnoreCase))
        {
            return "TAB";
        }

        if (string.Equals(key.KeyName, "CAPS LOCK", StringComparison.OrdinalIgnoreCase))
        {
            return "CAPS LOCK";
        }

        if (string.Equals(key.KeyName, "OEM102", StringComparison.OrdinalIgnoreCase))
        {
            return "OEM102";
        }

        if (string.Equals(key.KeyName, "ENTER", StringComparison.OrdinalIgnoreCase))
        {
            return key.RowIndex == 4 ? "ENTER-NUMPAD" : "ENTER";
        }

        if (string.Equals(key.KeyName, "SHIFT", StringComparison.OrdinalIgnoreCase))
        {
            return key.ColumnIndex < 5 ? "SHIFT-L" : "SHIFT-R";
        }

        return null;
    }

    private static KeyboardCluster GetCluster(KeyboardKey key)
    {
        if (string.Equals(key.KeyName, "ESC", StringComparison.OrdinalIgnoreCase))
        {
            return KeyboardCluster.EscCluster;
        }

        if (IsFunctionKey(key.KeyName, 1, 4))
        {
            return KeyboardCluster.F1ToF4Cluster;
        }

        if (IsFunctionKey(key.KeyName, 5, 8))
        {
            return KeyboardCluster.F5ToF8Cluster;
        }

        if (IsFunctionKey(key.KeyName, 9, 12))
        {
            return KeyboardCluster.F9ToF12Cluster;
        }

        if (string.Equals(key.KeyName, "PRINT SCREEN", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key.KeyName, "SCROLL LOCK", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key.KeyName, "PAUSE", StringComparison.OrdinalIgnoreCase))
        {
            return KeyboardCluster.PrintScrollPauseCluster;
        }

        if (string.Equals(key.KeyName, "INSERT", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key.KeyName, "HOME", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key.KeyName, "PAGE UP", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key.KeyName, "DELETE", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key.KeyName, "END", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key.KeyName, "PAGE DOWN", StringComparison.OrdinalIgnoreCase))
        {
            return KeyboardCluster.NavigationCluster;
        }

        if (string.Equals(key.KeyName, "LEFT", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key.KeyName, "DOWN", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key.KeyName, "RIGHT", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key.KeyName, "UP", StringComparison.OrdinalIgnoreCase))
        {
            return KeyboardCluster.ArrowCluster;
        }

        if (key.KeyName is "NUM LOCK" or "/" or "*" or "-" or "+" or "."
            || (char.IsDigit(key.KeyName.FirstOrDefault()) && key.RowIndex >= 2))
        {
            return KeyboardCluster.NumpadCluster;
        }

        return KeyboardCluster.MainTypingCluster;
    }

    private static bool IsFunctionKey(string keyName, int start, int end)
    {
        if (!keyName.StartsWith("F", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return int.TryParse(keyName[1..], out var number) && number >= start && number <= end;
    }

    private static void NotifyCalibrationChanged()
    {
        CalibrationChanged?.Invoke();
    }

    private void KeyboardLayoutPanel_Loaded(object sender, RoutedEventArgs e)
    {
        CalibrationChanged += HandleCalibrationChanged;
        KeyboardClusterLayout.Changed += HandleCalibrationChanged;
    }

    private void KeyboardLayoutPanel_Unloaded(object sender, RoutedEventArgs e)
    {
        CalibrationChanged -= HandleCalibrationChanged;
        KeyboardClusterLayout.Changed -= HandleCalibrationChanged;
    }

    private void HandleCalibrationChanged()
    {
        InvalidateMeasure();
        InvalidateArrange();
        UpdateLayout();
    }

    private static double GetKeyRightEdge(KeyboardKey key)
    {
        var clusterCalibration = KeyboardClusterLayout.Get(GetCluster(key));
        var specialOverride = GetSpecialOverride(key);
        var keyOverride = GetPerKeyOverride(key);
        var baseWidth = Math.Max(1d, (key.WidthUnits * KeyUnit) + specialOverride.WidthAdjustment + keyOverride.WidthAdjustment);
        var width = Math.Max(1d, baseWidth * ButtonScale);
        var x = OffsetX + clusterCalibration.OffsetX + key.ColumnIndex * (KeyUnit + GapX) + keyOverride.OffsetX + ((baseWidth - width) / 2d);
        return x + width;
    }

    private static double GetKeyBottomEdge(KeyboardKey key)
    {
        var clusterCalibration = KeyboardClusterLayout.Get(GetCluster(key));
        var keyOverride = GetPerKeyOverride(key);
        var baseHeight = Math.Max(1d, (key.HeightUnits * KeyUnit) + keyOverride.HeightAdjustment);
        var height = Math.Max(1d, baseHeight * ButtonScale);
        var y = OffsetY + clusterCalibration.OffsetY + key.RowIndex * (KeyUnit + GapY) + keyOverride.OffsetY + ((baseHeight - height) / 2d);
        return y + height;
    }

    private readonly record struct SpecialKeyOverride(double WidthAdjustment);
    private readonly record struct PerKeyOverride(double OffsetX, double OffsetY, double WidthAdjustment, double HeightAdjustment);
}
