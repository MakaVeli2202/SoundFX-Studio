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
    private static double _gap = 3;
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

    public static double Gap
    {
        get => _gap;
        set
        {
            if (Math.Abs(_gap - value) < double.Epsilon)
            {
                return;
            }

            _gap = value;
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

    public static void SetLayoutCalibration(double keyUnit, double gap, double offsetX, double offsetY)
    {
        _keyUnit = keyUnit;
        _gap = gap;
        _offsetX = offsetX;
        _offsetY = offsetY;
        NotifyCalibrationChanged();
    }

    public static void SetSpecialKeyOverride(string keyId, double widthAdjustment)
    {
        SpecialKeyOverrides[keyId] = new SpecialKeyOverride(widthAdjustment);
        NotifyCalibrationChanged();
    }

    public static void ApplyStarterCalibration()
    {
        ClearAllSpecialKeyOverrides();
    }

    public static void ConfigureCalibration(Action<KeyboardCalibrationBuilder> configure)
    {
        var builder = new KeyboardCalibrationBuilder();
        configure(builder);
        ClearAllSpecialKeyOverrides();
        builder.ApplyTo(SetSpecialKeyOverride);
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

    // IMPORTANT:
    //
    // Global alignment must be achieved using:
    //
    // KeyUnit
    // Gap
    // OffsetX
    // OffsetY
    //
    // Special-key overrides are reserved only for
    // physically different keys such as:
    //
    // Spacebar
    // Backspace
    // Enter
    // ISO Enter
    // Left Shift
    // Right Shift
    // Numpad Enter
    // Tab
    // Caps Lock

    public sealed class KeyboardCalibrationBuilder
    {
        private readonly List<SpecialKeyOverrideEntry> _entries = new();

        public KeyboardCalibrationBuilder Spacebar(double widthAdjustment) => Add("SPACE", widthAdjustment);
        public KeyboardCalibrationBuilder Backspace(double widthAdjustment) => Add("BACKSPACE", widthAdjustment);
        public KeyboardCalibrationBuilder Enter(double widthAdjustment) => Add("ENTER", widthAdjustment);
        public KeyboardCalibrationBuilder IsoEnter(double widthAdjustment) => Add("OEM102", widthAdjustment);
        public KeyboardCalibrationBuilder LeftShift(double widthAdjustment) => Add("SHIFT-L", widthAdjustment);
        public KeyboardCalibrationBuilder RightShift(double widthAdjustment) => Add("SHIFT-R", widthAdjustment);
        public KeyboardCalibrationBuilder NumpadEnter(double widthAdjustment) => Add("ENTER-NUMPAD", widthAdjustment);
        public KeyboardCalibrationBuilder Tab(double widthAdjustment) => Add("TAB", widthAdjustment);
        public KeyboardCalibrationBuilder CapsLock(double widthAdjustment) => Add("CAPS LOCK", widthAdjustment);

        internal void ApplyTo(Action<string, double> register)
        {
            foreach (var entry in _entries)
            {
                register(entry.KeyId, entry.WidthAdjustment);
            }
        }

        private KeyboardCalibrationBuilder Add(string keyId, double widthAdjustment)
        {
            _entries.Add(new SpecialKeyOverrideEntry(keyId, widthAdjustment));
            return this;
        }

        private readonly record struct SpecialKeyOverrideEntry(string KeyId, double WidthAdjustment);
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

        var desiredWidth = Math.Max(BaseLayoutWidth, Math.Max(0d, maxWidth + Gap));
        var desiredHeight = Math.Max(BaseLayoutHeight, Math.Max(0d, maxHeight + Gap));

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

            if (DebugKeyboardCalibration && child is Button button)
            {
                button.Background = new SolidColorBrush(Color.FromArgb(40, 255, 0, 0));
                button.BorderBrush = Brushes.Red;
                button.BorderThickness = new Thickness(1);
                button.Opacity = 1;
            }

            var cluster = GetCluster(key);
            var clusterCalibration = KeyboardClusterLayout.Get(cluster);
            var specialOverride = GetSpecialOverride(key);
            var keyOverride = GetPerKeyOverride(key);
            var baseWidth = Math.Max(1d, (key.WidthUnits * KeyUnit) + specialOverride.WidthAdjustment + keyOverride.WidthAdjustment);
            var baseHeight = Math.Max(1d, (key.HeightUnits * KeyUnit) + keyOverride.HeightAdjustment);
            var width = Math.Max(1d, baseWidth * ButtonScale);
            var height = Math.Max(1d, baseHeight * ButtonScale);

            // Keep scaled keys centered within their logical slot so global spacing stays stable.
            var x = OffsetX + clusterCalibration.OffsetX + key.ColumnIndex * (KeyUnit + Gap) + keyOverride.OffsetX + ((baseWidth - width) / 2d);
            var y = OffsetY + clusterCalibration.OffsetY + key.RowIndex * (KeyUnit + Gap) + keyOverride.OffsetY + ((baseHeight - height) / 2d);

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
            Math.Max(BaseLayoutWidth, Math.Max(0d, maxWidth + Gap)),
            Math.Max(BaseLayoutHeight, Math.Max(0d, maxHeight + Gap)));
    }

    private static SpecialKeyOverride GetSpecialOverride(KeyboardKey key)
    {
        var overrideKey = GetSpecialOverrideKey(key);
        return overrideKey is null || !SpecialKeyOverrides.TryGetValue(overrideKey, out var calibration)
            ? default
            : calibration;
    }

    private static PerKeyOverride GetPerKeyOverride(KeyboardKey key)
        => string.IsNullOrWhiteSpace(key.Id) || !PerKeyOverrides.TryGetValue(key.Id, out var calibration)
            ? default
            : calibration;

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

        if (IsPrintScrollPauseKey(key.KeyName))
        {
            return KeyboardCluster.PrintScrollPauseCluster;
        }

        if (IsNavigationKey(key.KeyName))
        {
            return KeyboardCluster.NavigationCluster;
        }

        if (IsArrowKey(key.KeyName))
        {
            return KeyboardCluster.ArrowCluster;
        }

        if (IsNumpadKey(key))
        {
            return KeyboardCluster.NumpadCluster;
        }

        return KeyboardCluster.MainTypingCluster;
    }

    private static bool IsFunctionKey(string keyName, int first, int last)
    {
        if (!keyName.StartsWith('F'))
        {
            return false;
        }

        return int.TryParse(keyName[1..], out var number) && number >= first && number <= last;
    }

    private static bool IsNavigationKey(string keyName)
        => keyName is "INSERT" or "HOME" or "PAGE UP" or "DELETE" or "END" or "PAGE DOWN";

    private static bool IsPrintScrollPauseKey(string keyName)
        => keyName is "PRINT SCREEN" or "SCROLL LOCK" or "PAUSE";

    private static bool IsArrowKey(string keyName)
        => keyName is "LEFT" or "DOWN" or "RIGHT" or "UP";

    private static bool IsNumpadKey(KeyboardKey key)
        => key.RowIndex >= 1 && key.ColumnIndex >= 16.25;

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
        var x = OffsetX + clusterCalibration.OffsetX + key.ColumnIndex * (KeyUnit + Gap) + keyOverride.OffsetX + ((baseWidth - width) / 2d);
        return x + width;
    }

    private static double GetKeyBottomEdge(KeyboardKey key)
    {
        var clusterCalibration = KeyboardClusterLayout.Get(GetCluster(key));
        var keyOverride = GetPerKeyOverride(key);
        var baseHeight = Math.Max(1d, (key.HeightUnits * KeyUnit) + keyOverride.HeightAdjustment);
        var height = Math.Max(1d, baseHeight * ButtonScale);
        var y = OffsetY + clusterCalibration.OffsetY + key.RowIndex * (KeyUnit + Gap) + keyOverride.OffsetY + ((baseHeight - height) / 2d);
        return y + height;
    }

    private readonly record struct SpecialKeyOverride(double WidthAdjustment);
    private readonly record struct PerKeyOverride(double OffsetX, double OffsetY, double WidthAdjustment, double HeightAdjustment);
}