using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace SoundFXStudio.UI.Tests;

[Collection("App")]
public class PresetsTabTests
{
    private readonly AppFixture _app;

    public PresetsTabTests(AppFixture app) => _app = app;

    private void NavigateToPresets()
    {
        var win = _app.GetMainWindow();
        var tab = win.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tab));
        Assert.NotNull(tab);
        var presetsTab = tab.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.TabItem).And(cf.ByName("Presets")));
        Assert.NotNull(presetsTab);
        presetsTab.Click();
        Thread.Sleep(500);
    }

    [Fact]
    public void PresetsTab_HasProfilesList()
    {
        NavigateToPresets();
        var win = _app.GetMainWindow();
        var lists = win.FindAllDescendants(cf => cf.ByControlType(ControlType.List));
        Assert.True(lists.Length > 0, "Presets tab should have a profiles list");
    }

    [Fact]
    public void PresetsTab_HasNewButton()
    {
        NavigateToPresets();
        var win = _app.GetMainWindow();
        var buttons = win.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
        var newBtn = buttons.FirstOrDefault(b =>
            b.Name.Contains("New", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(newBtn);
    }

    [Fact]
    public void PresetsTab_HasSearchBox()
    {
        NavigateToPresets();
        var win = _app.GetMainWindow();
        var textBoxes = win.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit));
        Assert.True(textBoxes.Length > 0, "Presets tab should have a search box");
    }
}
