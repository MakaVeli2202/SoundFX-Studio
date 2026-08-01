using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace SoundFXStudio.Views.Dialogs;

public partial class HotkeyCaptureDialog : Window, INotifyPropertyChanged
{
    private bool _isRecording;
    private bool _hasCapturedShortcut;
    private string _capturedHotkey = string.Empty;

    public HotkeyCaptureDialog(string? initialHotkey = null)
    {
        InitializeComponent();
        DataContext = this;
        CapturedHotkey = string.IsNullOrWhiteSpace(initialHotkey) ? "Press Record" : initialHotkey.Trim().ToUpperInvariant();
    }

    public string CapturedHotkey
    {
        get => _capturedHotkey;
        private set => SetProperty(ref _capturedHotkey, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        RecordButton.Focus();
        Keyboard.Focus(RecordButton);
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is System.Windows.Shapes.Path or System.Windows.Controls.Canvas)
            return;
        if (e.ClickCount == 2)
            return;
        DragMove();
    }

    private void RecordButton_Click(object sender, RoutedEventArgs e)
    {
        _isRecording = true;
        _hasCapturedShortcut = false;
        DoneButton.IsEnabled = false;
        CapturedHotkey = "Listening...";
        Keyboard.Focus(RecordButton);
    }

    private void DoneButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_hasCapturedShortcut || string.IsNullOrWhiteSpace(CapturedHotkey) || string.Equals(CapturedHotkey, "Listening...", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        DialogResult = true;
        Close();
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
            DoneButton_Click(sender, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (!_isRecording)
        {
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
        CapturedHotkey = string.Join("+", parts);
        _hasCapturedShortcut = true;
        _isRecording = false;
        DoneButton.IsEnabled = true;
        DoneButton.Focus();
        e.Handled = true;
    }

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (!Equals(field, value))
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}