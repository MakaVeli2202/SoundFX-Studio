using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using System.Runtime.InteropServices;

namespace SoundFXStudio.UI.Tests;

[Collection("App")]
public class TempDiagTests
{
    private readonly AppFixture _app;

    public TempDiagTests(AppFixture app) => _app = app;

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, char[] lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const uint WM_CLOSE = 0x0010;

    private static IntPtr FindWindowByTitle(string title)
    {
        var found = IntPtr.Zero;
        EnumWindows((h, l) =>
        {
            if (IsWindowVisible(h))
            {
                var buf = new char[256];
                GetWindowText(h, buf, 256);
                if (new string(buf).TrimEnd('\0') == title)
                {
                    found = h;
                    return false;
                }
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    [Fact]
    public void DumpWindows()
    {
        var win = _app.GetMainWindow();
        var home = win.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Button).And(cf.ByName("Home")))?.AsButton();
        Assert.NotNull(home);
        home!.Click();
        Thread.Sleep(800);

        var open = win.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Button).And(cf.ByAutomationId("OpenKeyboardButton")))?.AsButton();
        Assert.NotNull(open);
        open.Click();
        Thread.Sleep(1500);

        var hwnd = FindWindowByTitle("Keyboard");
        Assert.NotEqual(IntPtr.Zero, hwnd);

        var kb = _app.Automation.FromHandle(hwnd);
        var lines = new List<string> { $"FromHandle Name='{kb.Name}' Class={kb.ClassName}" };

        var buttons = kb.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
        lines.Add($"buttons={buttons.Length}");
        lines.Add("sample names: " + string.Join(", ", buttons.Take(10).Select(b => $"'{b.Name}'")));

        var images = kb.FindAllDescendants(cf => cf.ByControlType(ControlType.Image));
        lines.Add($"images={images.Length}");

        SendMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        Thread.Sleep(1000);
        lines.Add($"after WM_CLOSE IsWindow={IsWindow(hwnd)}");

        Assert.Fail(string.Join("\n", lines));
    }
}
