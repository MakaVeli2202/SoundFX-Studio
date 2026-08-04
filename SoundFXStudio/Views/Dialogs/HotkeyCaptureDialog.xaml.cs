using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SoundFXStudio.Views.Dialogs;

public partial class HotkeyCaptureDialog : Window
{
    private bool _hasCapturedShortcut;

    public HotkeyCaptureDialog(string? initialHotkey = null)
    {
        InitializeComponent();
        _hasCapturedShortcut = !string.IsNullOrWhiteSpace(initialHotkey);
        if (_hasCapturedShortcut)
        {
            UpdateDisplay(initialHotkey!.Trim().ToUpperInvariant());
            SaveButton.IsEnabled = true;
            ClearButton.IsEnabled = true;
            InstructionText.Text = "Shortcut loaded. Press a key to change it, or Save to confirm.";
        }

        Loaded += (_, _) =>
        {
            Keyboard.Focus(this);
            StartListeningAnimation();
        };
    }

    public string CapturedHotkey { get; private set; } = string.Empty;

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

    private void UpdateDisplay(string hotkey)
    {
        var parts = hotkey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var modifiers = parts.Where(part => part is "CTRL" or "SHIFT" or "ALT" or "WIN").ToList();
        var keyParts = parts.Where(part => part is not ("CTRL" or "SHIFT" or "ALT" or "WIN")).ToList();

        ModifierText.Text = modifiers.Count > 0 ? string.Join(" + ", modifiers) : string.Empty;
        CapturedKeyText.Text = keyParts.Count > 0 ? string.Join(" + ", keyParts) : hotkey;
        CapturedHotkey = hotkey;
        _hasCapturedShortcut = true;
        SaveButton.IsEnabled = true;
        ClearButton.IsEnabled = true;
        StopListeningAnimation();
        InstructionText.Text = "Press Save to confirm, or Clear to reset.";

        var flash = new ColorAnimation
        {
            To = Color.FromRgb(0x00, 0xD8, 0xFF),
            Duration = new Duration(TimeSpan.FromMilliseconds(300)),
            AutoReverse = true
        };
        KeyDisplayBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x70, 0xAF));
        KeyDisplayBorder.BorderBrush.BeginAnimation(SolidColorBrush.ColorProperty, flash);
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is System.Windows.Shapes.Path or System.Windows.Controls.Canvas)
            return;
        if (e.ClickCount == 2)
            return;
        DragMove();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_hasCapturedShortcut || string.IsNullOrWhiteSpace(CapturedHotkey))
        {
            return;
        }

        DialogResult = true;
        Close();
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        ModifierText.Text = string.Empty;
        CapturedKeyText.Text = "Listening...";
        ChordHintText.Text = string.Empty;
        CapturedHotkey = string.Empty;
        _hasCapturedShortcut = false;
        SaveButton.IsEnabled = false;
        ClearButton.IsEnabled = false;
        InstructionText.Text = "Press a key combination to assign...";
        KeyDisplayBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x70, 0xAF));
        StartListeningAnimation();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CancelButton_Click(sender, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Tab)
        {
            return;
        }

        if (_hasCapturedShortcut && e.Key == Key.Return)
        {
            SaveButton_Click(sender, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
        {
            return;
        }

        if (e.Key == Key.Return)
        {
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;
        var parts = new List<string>();

        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            parts.Add("CTRL");
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            parts.Add("SHIFT");
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            parts.Add("ALT");
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            parts.Add("WIN");
        }

        var keyText = key switch
        {
            Key.Escape => "ESC",
            Key.Back => "BACKSPACE",
            Key.Tab => "TAB",
            Key.CapsLock => "CAPS LOCK",
            Key.LeftShift or Key.RightShift => "SHIFT",
            Key.LeftCtrl or Key.RightCtrl => "CTRL",
            Key.LeftAlt or Key.RightAlt => "ALT",
            Key.LWin or Key.RWin => "WIN",
            Key.Apps => "MENU",
            Key.PrintScreen => "PRINT SCREEN",
            Key.Scroll => "SCROLL LOCK",
            Key.Pause => "PAUSE",
            Key.NumLock => "NUM LOCK",
            Key.PageUp => "PAGE UP",
            Key.PageDown => "PAGE DOWN",
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
            Key.Space => "SPACE",
            Key.Return => "ENTER",
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
            _ => key.ToString().ToUpperInvariant()
        };

        parts.Add(keyText);
        UpdateDisplay(string.Join("+", parts));
        e.Handled = true;
    }
}
