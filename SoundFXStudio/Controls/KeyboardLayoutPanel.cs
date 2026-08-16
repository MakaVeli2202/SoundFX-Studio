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
    private static readonly Dictionary<int, RowOffset> RowOffsets = new();
    private static readonly Dictionary<KeyboardCluster, GapOverride> ClusterGaps = new();
    private static readonly Dictionary<int, GapOverride> RowGaps = new();
    private static readonly Dictionary<string, KeyBaseline> KeyBaselines = new(StringComparer.OrdinalIgnoreCase);
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

    public static void SetRowCalibration(int rowIndex, double offsetX, double offsetY)
    {
        RowOffsets[rowIndex] = new RowOffset(offsetX, offsetY);
        NotifyCalibrationChanged();
    }

    public static void ResetRowCalibration()
    {
        RowOffsets.Clear();
        NotifyCalibrationChanged();
    }

    public static void SetClusterGapCalibration(KeyboardCluster cluster, double gapX, double gapY)
    {
        ClusterGaps[cluster] = new GapOverride(gapX, gapY);
        NotifyCalibrationChanged();
    }

    public static void ClearClusterGapCalibration()
    {
        ClusterGaps.Clear();
        NotifyCalibrationChanged();
    }

    public static void SetRowGapCalibration(int rowIndex, double gapX, double gapY)
    {
        RowGaps[rowIndex] = new GapOverride(gapX, gapY);
        NotifyCalibrationChanged();
    }

    public static void ClearRowGapCalibration()
    {
        RowGaps.Clear();
        NotifyCalibrationChanged();
    }

    public static void SetKeyBaseline(string keyId, double x, double y, double width, double height)
    {
        KeyBaselines[keyId] = new KeyBaseline(x, y, width, height);
    }

    public static void ClearKeyBaselines()
    {
        KeyBaselines.Clear();
        NotifyCalibrationChanged();
    }

    public static KeyboardCluster GetClusterOf(KeyboardKey key) => GetCluster(key);

    public static (double X, double Y, double Width, double Height)? GetKeyGeometry(KeyboardKey key)
    {
        var clusterCalibration = KeyboardClusterLayout.Get(GetCluster(key));
        var size = GetKeySize(key, clusterCalibration);
        var topLeft = GetKeyTopLeft(key, clusterCalibration, size);
        return (topLeft.X, topLeft.Y, size.Width, size.Height);
    }

    public static IReadOnlyDictionary<string, (double X, double Y, double Width, double Height)> GetKeyBaselinesSnapshot()
    {
        return KeyBaselines.ToDictionary(
            kv => kv.Key,
            kv => (kv.Value.X, kv.Value.Y, kv.Value.Width, kv.Value.Height),
            StringComparer.OrdinalIgnoreCase);
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
            if (child is FrameworkElement element && element.DataContext is KeyboardKey key)
            {
                var slot = GetKeySlotSize(key);
                child.Measure(slot);
            }
            else
            {
                child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            }
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
            var size = GetKeySize(key, clusterCalibration);
            var topLeft = GetKeyTopLeft(key, clusterCalibration, size);

            child.Arrange(new Rect(topLeft, size));
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

    private static Size GetKeySlotSize(KeyboardKey key)
    {
        var clusterCalibration = KeyboardClusterLayout.Get(GetCluster(key));
        return GetKeySize(key, clusterCalibration);
    }

    private static Size GetKeySize(KeyboardKey key, KeyboardClusterCalibration clusterCalibration)
    {
        if (GetBaseline(key) is KeyBaseline baseline)
        {
            var baselineKeyOverride = GetPerKeyOverride(key);
            return new Size(
                Math.Max(1d, baseline.Width + clusterCalibration.WidthAdjustment + baselineKeyOverride.WidthAdjustment),
                Math.Max(1d, baseline.Height + clusterCalibration.HeightAdjustment));
        }

        var specialOverride = GetSpecialOverride(key);
        var keyOverride = GetPerKeyOverride(key);
        var baseWidth = Math.Max(1d, (key.WidthUnits * KeyUnit) + specialOverride.WidthAdjustment + keyOverride.WidthAdjustment);
        var baseHeight = Math.Max(1d, (key.HeightUnits * KeyUnit) + keyOverride.HeightAdjustment);
        return new Size(
            Math.Max(1d, (baseWidth * ButtonScale) + clusterCalibration.WidthAdjustment),
            Math.Max(1d, (baseHeight * ButtonScale) + clusterCalibration.HeightAdjustment));
    }

    private static Point GetKeyTopLeft(KeyboardKey key, KeyboardClusterCalibration clusterCalibration, Size size)
    {
        var rowOffset = GetRowOffset(key);
        var keyOverride = GetPerKeyOverride(key);

        if (GetBaseline(key) is KeyBaseline baseline)
        {
            return new Point(
                baseline.X + clusterCalibration.OffsetX + rowOffset.OffsetX + keyOverride.OffsetX,
                baseline.Y + clusterCalibration.OffsetY + rowOffset.OffsetY + keyOverride.OffsetY);
        }

        var (gapX, gapY) = GetEffectiveGaps(key, clusterCalibration);
        var specialOverride = GetSpecialOverride(key);
        var baseWidth = Math.Max(1d, (key.WidthUnits * KeyUnit) + specialOverride.WidthAdjustment + keyOverride.WidthAdjustment);
        var baseHeight = Math.Max(1d, (key.HeightUnits * KeyUnit) + keyOverride.HeightAdjustment);
        var x = OffsetX + clusterCalibration.OffsetX + rowOffset.OffsetX + key.ColumnIndex * (KeyUnit + gapX) + keyOverride.OffsetX + ((baseWidth - size.Width) / 2d);
        var y = OffsetY + clusterCalibration.OffsetY + rowOffset.OffsetY + key.RowIndex * (KeyUnit + gapY) + keyOverride.OffsetY + ((baseHeight - size.Height) / 2d);
        return new Point(x, y);
    }

    private static (double GapX, double GapY) GetEffectiveGaps(KeyboardKey key, KeyboardClusterCalibration clusterCalibration)
    {
        var gapX = GapX + clusterCalibration.GapX;
        var gapY = GapY + clusterCalibration.GapY;

        if (GetCluster(key) == KeyboardCluster.MainLettersCluster
            && RowGaps.TryGetValue(key.RowIndex, out var rowGap))
        {
            gapX += rowGap.GapX;
            gapY += rowGap.GapY;
        }

        return (gapX, gapY);
    }

    private static KeyBaseline? GetBaseline(KeyboardKey key)
        => string.IsNullOrWhiteSpace(key.Id) || !KeyBaselines.TryGetValue(key.Id, out var baseline)
            ? null
            : baseline;

    private static PerKeyOverride GetPerKeyOverride(KeyboardKey key)
        => string.IsNullOrWhiteSpace(key.Id) || !PerKeyOverrides.TryGetValue(key.Id, out var calibration)
            ? default
            : calibration;

    private static RowOffset GetRowOffset(KeyboardKey key)
    {
        if (GetCluster(key) != KeyboardCluster.MainLettersCluster || !RowOffsets.TryGetValue(key.RowIndex, out var offset))
        {
            return default;
        }

        return offset;
    }

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

        if (key.ColumnIndex >= 20.25
            && (key.KeyName is "NUM LOCK" or "/" or "*" or "-" or "+" or "." or "ENTER"
                || char.IsDigit(key.KeyName.FirstOrDefault())))
        {
            return KeyboardCluster.NumpadCluster;
        }

        if (IsSpecialTypingKey(key))
        {
            return KeyboardCluster.MainTypingCluster;
        }

        return KeyboardCluster.MainLettersCluster;
    }

    private static bool IsSpecialTypingKey(KeyboardKey key)
        => key.KeyName is "TAB" or "CAPS LOCK" or "ENTER" or "BACKSPACE"
            or "SHIFT" or "CTRL" or "WIN" or "ALT" or "MENU" or "SPACE";

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
        var size = GetKeySize(key, clusterCalibration);
        var topLeft = GetKeyTopLeft(key, clusterCalibration, size);
        return topLeft.X + size.Width;
    }

    private static double GetKeyBottomEdge(KeyboardKey key)
    {
        var clusterCalibration = KeyboardClusterLayout.Get(GetCluster(key));
        var size = GetKeySize(key, clusterCalibration);
        var topLeft = GetKeyTopLeft(key, clusterCalibration, size);
        return topLeft.Y + size.Height;
    }

    private readonly record struct SpecialKeyOverride(double WidthAdjustment);
    private readonly record struct PerKeyOverride(double OffsetX, double OffsetY, double WidthAdjustment, double HeightAdjustment);
    private readonly record struct RowOffset(double OffsetX, double OffsetY);
    private readonly record struct GapOverride(double GapX, double GapY);
    private readonly record struct KeyBaseline(double X, double Y, double Width, double Height);
}
