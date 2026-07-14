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

        var keyText = e.Key switch
        {
            Key.Escape => "ESC",
            Key.Space => "SPACE",
            Key.Return => "ENTER",
            Key.Back => "BACKSPACE",
            Key.Tab => "TAB",
            Key.Oem102 => "OEM102",
            _ => e.Key.ToString().ToUpperInvariant()
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