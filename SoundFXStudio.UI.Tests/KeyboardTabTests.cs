using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace SoundFXStudio.UI.Tests;

[Collection("App")]
public class KeyboardTabTests
{
    private readonly AppFixture _app;

    public KeyboardTabTests(AppFixture app) => _app = app;

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
    public void KeyboardTab_ShowsKeyboardButtons()
    {
        OpenFloatingKeyboard();
        var kbWin = FindKeyboardWindow();
        Assert.NotNull(kbWin);
        var buttons = kbWin.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
        Assert.True(buttons.Length > 50, "Keyboard tab should render many key buttons");
    }

    [Fact]
    public void KeyboardTab_HasKeyboardBackgroundImage()
    {
        OpenFloatingKeyboard();
        var kbWin = FindKeyboardWindow();
        Assert.NotNull(kbWin);
        var images = kbWin.FindAllDescendants(cf => cf.ByControlType(ControlType.Image));
        Assert.True(images.Length > 0, "Keyboard tab should show keyboard background image");
    }
}
