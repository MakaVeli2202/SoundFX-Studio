using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using System.Runtime.InteropServices;

namespace SoundFXStudio.UI.Tests;

[Collection("App")]
public class KeyboardTests : IDisposable
{
    private readonly AppFixture _app;
    private readonly UIA3Automation _automation;

    public KeyboardTests(AppFixture app)
    {
        _app = app;
        _automation = _app.Automation;
    }

    public void Dispose()
    {
        try { CloseAllKeyboardWindows(); } catch { }
    }

    private static bool WaitUntil(Func<bool> condition, int timeoutMs = 5000, int intervalMs = 100)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (condition())
                {
                    return true;
                }
            }
            catch { }
            Thread.Sleep(intervalMs);
        }
        return false;
    }

    private Window MainWindow() => _app.GetMainWindow();

    private void NavigateToHome()
    {
        var win = MainWindow();
        Button? home = null;
        WaitUntil(() => (home = win.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Button).And(cf.ByName("Home")))?.AsButton()) != null);
        Assert.NotNull(home);
        home!.Click();
    }

    private Button? FindOpenKeyboardButton(Window win)
    {
        var byId = win.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Button).And(cf.ByAutomationId("OpenKeyboardButton")))?.AsButton();
        if (byId is not null)
        {
            return byId;
        }
        return win.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Button).And(cf.ByName("Open Keyboard")))?.AsButton();
    }

    private Window OpenKeyboard()
    {
        CloseAllKeyboardWindows();
        NavigateToHome();

        var win = MainWindow();
        Button? openBtn = null;
        WaitUntil(() => (openBtn = FindOpenKeyboardButton(win)) != null);
        Assert.NotNull(openBtn);
        openBtn!.Click();

        IntPtr hwnd = IntPtr.Zero;
        WaitUntil(() => (hwnd = FindKeyboardHwnd()) != IntPtr.Zero);
        Assert.NotEqual(IntPtr.Zero, hwnd);
        return _automation.FromHandle(hwnd).AsWindow();
    }

    private static AutomationElement[] KeyButtons(Window kb) =>
        kb.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, char[] text, int maxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const uint WM_CLOSE = 0x0010;

    private static IntPtr FindKeyboardHwnd()
    {
        var found = IntPtr.Zero;
        EnumWindows((h, l) =>
        {
            if (IsWindowVisible(h))
            {
                var buf = new char[256];
                GetWindowText(h, buf, 256);
                if (new string(buf).TrimEnd('\0') == "Keyboard")
                {
                    found = h;
                    return false;
                }
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    private static void CloseKeyboardHwnd(IntPtr hwnd) =>
        SendMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

    private void CloseAllKeyboardWindows()
    {
        while (true)
        {
            var hwnd = FindKeyboardHwnd();
            if (hwnd == IntPtr.Zero)
            {
                return;
            }
            CloseKeyboardHwnd(hwnd);
            if (!WaitUntil(() => !IsWindow(hwnd), 3000))
            {
                return;
            }
        }
    }

    [Fact]
    public void KeyboardTab_HasOpenKeyboardButton()
    {
        NavigateToHome();
        var win = MainWindow();
        Button? openBtn = null;
        WaitUntil(() => (openBtn = FindOpenKeyboardButton(win)) != null);
        Assert.NotNull(openBtn);
    }

    [Fact]
    public void Keyboard_HasManyKeyButtons()
    {
        var kb = OpenKeyboard();
        var buttons = KeyButtons(kb);
        Assert.True(buttons.Length > 80, $"Should have 80+ keyboard buttons, found {buttons.Length}");
    }

    [Fact]
    public void Keyboard_HasKeyboardBackgroundImage()
    {
        var kb = OpenKeyboard();
        var images = kb.FindAllDescendants(cf => cf.ByControlType(ControlType.Image));
        Assert.True(images.Length > 0, "Keyboard should show background image");
    }

    [Fact]
    public void Keyboard_KeyButtons_HaveNames()
    {
        var kb = OpenKeyboard();
        var buttons = KeyButtons(kb);
        var named = buttons.Where(b => !string.IsNullOrWhiteSpace(b.Name)).ToList();
        Assert.True(named.Count > 30, "Key buttons should have accessible names");
    }

    [Theory]
    [InlineData("Escape")]
    [InlineData("F1")]
    [InlineData("Space")]
    [InlineData("Enter")]
    public void Keyboard_HasSpecialKey(string keyName)
    {
        var kb = OpenKeyboard();
        var buttons = KeyButtons(kb);
        string token = keyName switch
        {
            "Escape" => "ESC-",
            "Space" => "SPACE-",
            "Enter" => "ENTER-",
            _ => $"{keyName.ToUpperInvariant()}-"
        };

        var key = buttons.FirstOrDefault(b =>
            b.Name.Contains(token, StringComparison.OrdinalIgnoreCase)
            || b.Name.Equals(keyName, StringComparison.OrdinalIgnoreCase)
            || b.Name.Contains(keyName, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(key);
    }
}
