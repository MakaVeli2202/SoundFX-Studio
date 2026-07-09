using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;

namespace SoundFXStudio.Services;

public sealed class HotkeyService : IDisposable
{
    private const int WmHotKey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;

    private readonly Dictionary<string, Registration> _registrations = new(StringComparer.OrdinalIgnoreCase);
    private HwndSource? _source;
    private int _nextId = 1;

    public event EventHandler<HotkeyEventArgs>? HotkeyPressed;

    public void Attach(Window window)
    {
        var helper = new WindowInteropHelper(window);
        _source = HwndSource.FromHwnd(helper.Handle);
        _source?.AddHook(WndProc);
    }

    public bool Register(string ownerId, string hotkeyText)
    {
        if (!TryParse(hotkeyText, out var key, out var modifiers))
        {
            return false;
        }

        if (_registrations.Values.Any(registration => registration.Key == key && registration.Modifiers == modifiers))
        {
            return false;
        }

        Unregister(ownerId);

        var registration = new Registration
        {
            Id = _nextId++,
            OwnerId = ownerId,
            Key = key,
            Modifiers = modifiers,
            HotkeyText = Normalize(hotkeyText)
        };

        _registrations[ownerId] = registration;

        if (_source is null)
        {
            return true;
        }

        return RegisterHotKey(_source.Handle, registration.Id, modifiers, (uint)KeyInterop.VirtualKeyFromKey(key));
    }

    public void Unregister(string ownerId)
    {
        if (!_registrations.TryGetValue(ownerId, out var registration))
        {
            return;
        }

        if (_source is not null)
        {
            UnregisterHotKey(_source.Handle, registration.Id);
        }

        _registrations.Remove(ownerId);
    }

    public void Clear()
    {
        foreach (var ownerId in _registrations.Keys.ToList())
        {
            Unregister(ownerId);
        }
    }

    public void Dispose() => Clear();

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotKey)
        {
            var id = wParam.ToInt32();
            var registration = _registrations.Values.FirstOrDefault(item => item.Id == id);
            if (registration is not null)
            {
                HotkeyPressed?.Invoke(this, new HotkeyEventArgs(registration.OwnerId, registration.HotkeyText));
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    private static bool TryParse(string? hotkeyText, out Key key, out uint modifiers)
    {
        key = Key.None;
        modifiers = 0;

        if (string.IsNullOrWhiteSpace(hotkeyText))
        {
            return false;
        }

        var parts = hotkeyText.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= ModControl;
                    break;
                case "ALT":
                    modifiers |= ModAlt;
                    break;
                case "SHIFT":
                    modifiers |= ModShift;
                    break;
                case "WIN":
                case "WINDOWS":
                    modifiers |= ModWin;
                    break;
                default:
                    if (!TryParseKey(part, out key))
                    {
                        return false;
                    }
                    break;
            }
        }

        return key != Key.None;
    }

    private static bool TryParseKey(string input, out Key key)
    {
        key = Key.None;
        var normalized = input.Trim().ToUpperInvariant();

        if (normalized.Length == 1)
        {
            var ch = normalized[0];
            if (char.IsLetterOrDigit(ch))
            {
                key = (Key)Enum.Parse(typeof(Key), ch switch
                {
                    >= '0' and <= '9' => "D" + ch,
                    _ => ch.ToString()
                }, ignoreCase: true);
                return true;
            }
        }

        return Enum.TryParse(normalized, true, out key)
               || normalized switch
               {
                   "NUMPAD1" => TrySetKey(Key.NumPad1, out key),
                   "NUMPAD2" => TrySetKey(Key.NumPad2, out key),
                   "NUMPAD3" => TrySetKey(Key.NumPad3, out key),
                   "NUMPAD4" => TrySetKey(Key.NumPad4, out key),
                   "NUMPAD5" => TrySetKey(Key.NumPad5, out key),
                   "NUMPAD6" => TrySetKey(Key.NumPad6, out key),
                   "NUMPAD7" => TrySetKey(Key.NumPad7, out key),
                   "NUMPAD8" => TrySetKey(Key.NumPad8, out key),
                   "NUMPAD9" => TrySetKey(Key.NumPad9, out key),
                   "NUMPAD0" => TrySetKey(Key.NumPad0, out key),
                   _ => false
               };
    }

    private static bool TrySetKey(Key value, out Key key)
    {
        key = value;
        return true;
    }

    private static string Normalize(string hotkeyText) => string.Join("+", hotkeyText.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(part => part.Trim().ToUpperInvariant()));

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private sealed class Registration
    {
        public int Id { get; set; }
        public string OwnerId { get; set; } = string.Empty;
        public Key Key { get; set; }
        public uint Modifiers { get; set; }
        public string HotkeyText { get; set; } = string.Empty;
    }
}

public sealed class HotkeyEventArgs : EventArgs
{
    public HotkeyEventArgs(string ownerId, string hotkeyText)
    {
        OwnerId = ownerId;
        HotkeyText = hotkeyText;
    }

    public string OwnerId { get; }

    public string HotkeyText { get; }
}