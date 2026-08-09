using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SoundFXStudio.Views.Dialogs;

public partial class KeyCaptureDialog : Window
{
    private bool _captured;

    public KeyCaptureDialog(bool chordMode = false, IReadOnlyList<string>? existingKeys = null)
    {
        ChordMode = chordMode;
        InitializeComponent();
        if (existingKeys is not null)
        {
            foreach (var keyName in existingKeys)
            {
                if (!string.IsNullOrWhiteSpace(keyName))
                {
                    KeyNames.Add(keyName.Trim().ToUpperInvariant());
                }
            }
        }

        Loaded += (_, _) =>
        {
            if (KeyNames.Count > 0)
            {
                _captured = true;
                CapturedKeyName = KeyNames[0];
                CapturedChordKeyName = KeyNames.Count > 1 ? KeyNames[^1] : string.Empty;
                SaveButton.IsEnabled = true;
                ClearButton.IsEnabled = true;
                StopListeningAnimation();
                UpdateDisplay();
                InstructionText.Text = ChordMode
                    ? $"Current binding: {string.Join(" + ", KeyNames)}. Press keys to add (max 3), or Backspace to remove one."
                    : "Press Save to confirm, or Clear to reset.";
            }
            else
            {
                Keyboard.Focus(this);
                StartListeningAnimation();
            }
        };
    }

    public bool ChordMode { get; }
    public Key CapturedKey { get; private set; }
    public Key CapturedChordKey { get; private set; }
    public string CapturedKeyName { get; private set; } = string.Empty;
    public string CapturedChordKeyName { get; private set; } = string.Empty;
    public ModifierKeys CapturedModifiers { get; private set; }
    public List<string> KeyNames { get; } = new();

    private void StartListeningAnimation()
    {
        var pulse = new DoubleAnimation
        {
            From = 0.3,
            To = 1.0,
            Duration = new Duration(TimeSpan.FromMilliseconds(800)),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        KeyDisplayBorder.BeginAnimation(OpacityProperty, pulse);
    }

    private void StopListeningAnimation()
    {
        KeyDisplayBorder.BeginAnimation(OpacityProperty, null);
        KeyDisplayBorder.Opacity = 1.0;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape)
        {
            DialogResult = false;
            Close();
            return;
        }

        if (key is Key.Back or Key.Delete)
        {
            if (ChordMode && KeyNames.Count > 0)
            {
                KeyNames.RemoveAt(KeyNames.Count - 1);
                if (KeyNames.Count == 0)
                {
                    ClearButton_Click(sender, new RoutedEventArgs());
                }
                else
                {
                    CapturedKeyName = KeyNames[0];
                    CapturedChordKeyName = KeyNames.Count > 1 ? KeyNames[^1] : string.Empty;
                    CapturedChordKey = KeyNames.Count > 1 ? CapturedChordKey : Key.None;
                    _captured = true;
                    SaveButton.IsEnabled = true;
                    UpdateDisplay();
                    InstructionText.Text = "Press another key or press Save.";
                }
            }
            else
            {
                ClearButton_Click(sender, new RoutedEventArgs());
            }

            return;
        }

        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
        {
            return;
        }

        StopListeningAnimation();

        var mods = ModifierKeys.None;
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) mods |= ModifierKeys.Control;
        if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)) mods |= ModifierKeys.Alt;
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) mods |= ModifierKeys.Shift;
        if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin)) mods |= ModifierKeys.Windows;

        CapturedModifiers = mods;

        var name = KeyToName(key);

        if (ChordMode && _captured)
        {
            if (KeyNames.Count >= 3)
            {
                InstructionText.Text = "Maximum 3 keys reached — press Save, or Backspace to remove one.";
                return;
            }

            CapturedChordKey = key;
            CapturedChordKeyName = name;
            KeyNames.Add(name);
            UpdateDisplay();
            InstructionText.Text = "Combo captured. Press another key or Save.";
            return;
        }

        CapturedKey = key;
        CapturedKeyName = name;
        KeyNames.Add(name);
        _captured = true;
        SaveButton.IsEnabled = true;
        ClearButton.IsEnabled = true;
        UpdateDisplay();
        InstructionText.Text = ChordMode
            ? "Press another key for the chord combo, or press Save..."
            : "Press Save to confirm, or Clear to reset.";

        var flash = new ColorAnimation
        {
            To = Color.FromRgb(0x00, 0xD8, 0xFF),
            Duration = new Duration(TimeSpan.FromMilliseconds(300)),
            AutoReverse = true
        };
        KeyDisplayBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x70, 0xAF));
        KeyDisplayBorder.BorderBrush.BeginAnimation(SolidColorBrush.ColorProperty, flash);
    }

    private void UpdateDisplay()
    {
        var modText = ModifierKeysToString(CapturedModifiers);
        ModifierText.Text = modText;

        var parts = new List<string>();
        if (!string.IsNullOrEmpty(CapturedKeyName)) parts.Add(CapturedKeyName);
        if (ChordMode)
        {
            parts.AddRange(KeyNames.Skip(1));
        }
        CapturedKeyText.Text = string.Join(" + ", parts);

        if (ChordMode && parts.Count >= 3)
        {
            ChordHintText.Text = "Maximum 3 keys reached — press Save or Backspace to remove one.";
        }
        else if (ChordMode && parts.Count > 1)
        {
            ChordHintText.Text = $"Chord: press {string.Join(" + ", parts)} together";
        }
        else if (ChordMode && _captured)
        {
            ChordHintText.Text = "Press another key to extend the chord";
        }
        else
        {
            ChordHintText.Text = "";
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_captured) return;
        DialogResult = true;
        Close();
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        CapturedKey = Key.None;
        CapturedChordKey = Key.None;
        CapturedKeyName = string.Empty;
        CapturedChordKeyName = string.Empty;
        CapturedModifiers = ModifierKeys.None;
        KeyNames.Clear();
        CapturedKeyText.Text = "-";
        ModifierText.Text = "";
        ChordHintText.Text = "";
        _captured = false;
        SaveButton.IsEnabled = false;
        ClearButton.IsEnabled = false;
        InstructionText.Text = "Press a key to assign...";
        KeyDisplayBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x70, 0xAF));
        StartListeningAnimation();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is System.Windows.Shapes.Path or System.Windows.Controls.Canvas)
            return;
        if (e.ClickCount == 2)
            return;
        DragMove();
    }

    private static string ModifierKeysToString(ModifierKeys mods)
    {
        var parts = new List<string>();
        if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (mods.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (mods.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (mods.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        return parts.Count > 0 ? string.Join(" + ", parts) : "";
    }

    private static string KeyToName(Key key) => key switch
    {
        Key.Escape => "ESC",
        Key.Back => "BACKSPACE",
        Key.Tab => "TAB",
        Key.CapsLock => "CAPS LOCK",
        Key.PrintScreen => "PRINT SCREEN",
        Key.Scroll => "SCROLL LOCK",
        Key.Pause => "PAUSE",
        Key.NumLock => "NUM LOCK",
        Key.PageUp => "PAGE UP",
        Key.PageDown => "PAGE DOWN",
        Key.Home => "HOME",
        Key.End => "END",
        Key.Insert => "INSERT",
        Key.Delete => "DELETE",
        Key.Space => "SPACE",
        Key.Return => "ENTER",
        Key.D0 => "0",
        Key.D1 => "1",
        Key.D2 => "2",
        Key.D3 => "3",
        Key.D4 => "4",
        Key.D5 => "5",
        Key.D6 => "6",
        Key.D7 => "7",
        Key.D8 => "8",
        Key.D9 => "9",
        Key.NumPad0 => "NUMPAD0",
        Key.NumPad1 => "NUMPAD1",
        Key.NumPad2 => "NUMPAD2",
        Key.NumPad3 => "NUMPAD3",
        Key.NumPad4 => "NUMPAD4",
        Key.NumPad5 => "NUMPAD5",
        Key.NumPad6 => "NUMPAD6",
        Key.NumPad7 => "NUMPAD7",
        Key.NumPad8 => "NUMPAD8",
        Key.NumPad9 => "NUMPAD9",
        Key.Add => "+",
        Key.Subtract => "-",
        Key.Multiply => "*",
        Key.Divide => "/",
        Key.Decimal => ".",
        Key.OemTilde => "`",
        Key.OemMinus => "-",
        Key.OemPlus => "=",
        Key.OemOpenBrackets => "[",
        Key.Oem6 => "]",
        Key.Oem5 => "\\",
        Key.Oem1 => ";",
        Key.Oem7 => "'",
        Key.OemComma => ",",
        Key.OemPeriod => ".",
        Key.OemQuestion => "/",
        Key.Oem102 => "OEM102",
        Key.Left => "LEFT",
        Key.Right => "RIGHT",
        Key.Up => "UP",
        Key.Down => "DOWN",
        Key.LeftShift or Key.RightShift => "SHIFT",
        Key.LeftCtrl or Key.RightCtrl => "CTRL",
        Key.LeftAlt or Key.RightAlt => "ALT",
        Key.LWin or Key.RWin => "WIN",
        Key.Apps => "MENU",
        _ => key.ToString().ToUpperInvariant()
    };
}
