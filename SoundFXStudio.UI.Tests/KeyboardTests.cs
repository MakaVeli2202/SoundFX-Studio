using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

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
        try { RestoreMainWindow(); } catch { }
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

    private static bool IsMainWindow(Window w) =>
        (w.Name ?? string.Empty).Contains("SoundFX Studio", StringComparison.OrdinalIgnoreCase);

    private Window? FindKeyboardWindow()
    {
        try
        {
            foreach (var w in _app.App.GetAllTopLevelWindows(_automation))
            {
                if (!IsMainWindow(w))
                {
                    return w;
                }
            }
        }
        catch { }
        return null;
    }

    private void CloseKeyboardWindow(Window kb)
    {
        try
        {
            kb.Patterns.Window.Pattern?.SetWindowVisualState(WindowVisualState.Minimized);
        }
        catch { }

        try { kb.Close(); } catch { }

        try
        {
            kb.Patterns.Window.Pattern?.Close();
        }
        catch { }
    }

    private void CloseAllKeyboardWindows()
    {
        while (true)
        {
            var kb = FindKeyboardWindow();
            if (kb is null)
            {
                return;
            }
            CloseKeyboardWindow(kb);
            if (!WaitUntil(() => FindKeyboardWindow() is null, 3000))
            {
                return;
            }
        }
    }

    private void RestoreMainWindow()
    {
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(2);
            while (DateTime.UtcNow < deadline)
            {
                var main = _app.App.GetAllTopLevelWindows(_automation).FirstOrDefault(IsMainWindow);
                if (main is not null)
                {
                    try { main.Focus(); } catch { }
                    try { main.SetForeground(); } catch { }
                    return;
                }
                Thread.Sleep(100);
            }
        }
        catch { }
    }

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

        Window? kb = null;
        WaitUntil(() => (kb = FindKeyboardWindow()) != null);
        Assert.NotNull(kb);
        return kb!;
    }

    private static AutomationElement[] KeyButtons(Window kb) =>
        kb.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));

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
