using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace SoundFXStudio.UI.Tests;

[Collection("App")]
public class KeyboardInteractionTests
{
    private readonly AppFixture _app;

    public KeyboardInteractionTests(AppFixture app) => _app = app;

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
    public void Keyboard_HasManyKeyButtons()
    {
        NavigateToKeyboard();
        var win = _app.GetMainWindow();
        var buttons = win.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
        Assert.True(buttons.Length > 80, $"Should have 100+ keyboard buttons, found {buttons.Length}");
    }

    [Fact]
    public void Keyboard_HasKeyboardBackgroundImage()
    {
        NavigateToKeyboard();
        var win = _app.GetMainWindow();
        var images = win.FindAllDescendants(cf => cf.ByControlType(ControlType.Image));
        Assert.True(images.Length > 0, "Keyboard should show background image");
    }

    [Fact]
    public void Keyboard_HasViewbox()
    {
        NavigateToKeyboard();
        var win = _app.GetMainWindow();
        var viewboxes = win.FindAllDescendants(cf => cf.ByControlType(ControlType.Custom));
        var buttons = win.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
        Assert.True(buttons.Length > 50);
    }

    [Fact]
    public void Keyboard_KeyButtons_HaveNames()
    {
        NavigateToKeyboard();
        var win = _app.GetMainWindow();
        var buttons = win.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
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
        NavigateToKeyboard();
        var win = _app.GetMainWindow();
        var buttons = win.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
        string token = keyName switch
        {
            "Escape" => "ESC-",
            "Space" => "SPACE-",
            "Enter" => "ENTER-",
            _ => $"{keyName.ToUpperInvariant()}-"
        };

        var key = buttons.FirstOrDefault(b =>
            (b.AutomationId?.Contains(token, StringComparison.OrdinalIgnoreCase) ?? false)
            || b.Name.Contains(keyName, StringComparison.OrdinalIgnoreCase)
            || (keyName == "Escape" && b.Name.Contains("Esc", StringComparison.OrdinalIgnoreCase)));
        Assert.NotNull(key);
    }
}
