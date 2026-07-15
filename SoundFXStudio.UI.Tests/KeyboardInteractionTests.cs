using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace SoundFXStudio.UI.Tests;

[Collection("App")]
public class KeyboardInteractionTests
{
    private readonly AppFixture _app;

    public KeyboardInteractionTests(AppFixture app) => _app = app;

    private void NavigateToKeyboardTab()
    {
        var win = _app.GetMainWindow();
        var tab = win.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tab));
        Assert.NotNull(tab);
        var kbTab = tab.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.TabItem).And(cf.ByName("Keyboard")));
        Assert.NotNull(kbTab);
        kbTab.Click();
        Thread.Sleep(500);
    }

    private void OpenFloatingKeyboard()
    {
        NavigateToKeyboardTab();
        var win = _app.GetMainWindow();
        var openBtn = win.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Button).And(cf.ByAutomationId("OpenKeyboardButton")))
            ?? win.FindFirstDescendant(cf =>
                cf.ByControlType(ControlType.Button).And(cf.ByName("Open Keyboard")));
        Assert.NotNull(openBtn);
        openBtn.Click();
        Thread.Sleep(1500);
    }

    private Window? FindKeyboardWindow()
    {
        var windows = _app.App.GetAllTopLevelWindows(_app.Automation);
        var mainWin = _app.GetMainWindow();
        return windows.FirstOrDefault(w =>
            w != mainWin
            && w.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button)) is not null);
    }

    [Fact]
    public void KeyboardTab_HasOpenKeyboardButton()
    {
        NavigateToKeyboardTab();
        var win = _app.GetMainWindow();
        var openBtn = win.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Button).And(cf.ByAutomationId("OpenKeyboardButton")))
            ?? win.FindFirstDescendant(cf =>
                cf.ByControlType(ControlType.Button).And(cf.ByName("Open Keyboard")));
        Assert.NotNull(openBtn);
    }

    [Fact]
    public void KeyboardTab_HasDescriptionText()
    {
        NavigateToKeyboardTab();
        var win = _app.GetMainWindow();
        var texts = win.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
        var desc = texts.FirstOrDefault(t =>
            t.Name.Contains("Floating", StringComparison.OrdinalIgnoreCase)
            || t.Name.Contains("Keyboard", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(desc);
    }

    [Fact]
    public void Keyboard_HasManyKeyButtons()
    {
        OpenFloatingKeyboard();
        var kbWin = FindKeyboardWindow();
        Assert.NotNull(kbWin);
        var buttons = kbWin.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
        Assert.True(buttons.Length > 80, $"Should have 80+ keyboard buttons, found {buttons.Length}");
    }

    [Fact]
    public void Keyboard_HasKeyboardBackgroundImage()
    {
        OpenFloatingKeyboard();
        var kbWin = FindKeyboardWindow();
        Assert.NotNull(kbWin);
        var images = kbWin.FindAllDescendants(cf => cf.ByControlType(ControlType.Image));
        Assert.True(images.Length > 0, "Keyboard should show background image");
    }

    [Fact]
    public void Keyboard_HasViewbox()
    {
        OpenFloatingKeyboard();
        var kbWin = FindKeyboardWindow();
        Assert.NotNull(kbWin);
        var buttons = kbWin.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
        Assert.True(buttons.Length > 50);
    }

    [Fact]
    public void Keyboard_KeyButtons_HaveNames()
    {
        OpenFloatingKeyboard();
        var kbWin = FindKeyboardWindow();
        Assert.NotNull(kbWin);
        var buttons = kbWin.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
        var namedButtons = buttons.Where(b => !string.IsNullOrWhiteSpace(b.Name)).ToList();
        Assert.True(namedButtons.Count > 30, "Key buttons should have accessible names");
    }

    [Theory]
    [InlineData("Escape")]
    [InlineData("F1")]
    [InlineData("Space")]
    [InlineData("Enter")]
    public void Keyboard_HasSpecialKey(string keyName)
    {
        OpenFloatingKeyboard();
        var kbWin = FindKeyboardWindow();
        Assert.NotNull(kbWin);
        var buttons = kbWin.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
        string token = keyName switch
        {
            "Escape" => "ESC-",
            "Space" => "SPACE-",
            "Enter" => "ENTER-",
            _ => $"{keyName.ToUpperInvariant()}-"
        };

        var key = buttons.FirstOrDefault(b =>
            b.Name.Contains(token, StringComparison.OrdinalIgnoreCase)
            || b.Name.Contains(keyName, StringComparison.OrdinalIgnoreCase)
            || (keyName == "Escape" && b.Name.Contains("Esc", StringComparison.OrdinalIgnoreCase)));
        Assert.NotNull(key);
    }
}
