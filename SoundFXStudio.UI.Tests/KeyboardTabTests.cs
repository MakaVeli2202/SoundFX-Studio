using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace SoundFXStudio.UI.Tests;

[Collection("App")]
public class KeyboardTabTests
{
    private readonly AppFixture _app;

    public KeyboardTabTests(AppFixture app) => _app = app;

    private void NavigateToKeyboard()
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

    [Fact]
    public void KeyboardTab_ShowsKeyboardButtons()
    {
        NavigateToKeyboard();
        var win = _app.GetMainWindow();
        var buttons = win.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
        Assert.True(buttons.Length > 50, "Keyboard tab should render many key buttons");
    }

    [Fact]
    public void KeyboardTab_HasKeyboardBackgroundImage()
    {
        NavigateToKeyboard();
        var win = _app.GetMainWindow();
        var images = win.FindAllDescendants(cf => cf.ByControlType(ControlType.Image));
        Assert.True(images.Length > 0, "Keyboard tab should show keyboard background image");
    }
}
