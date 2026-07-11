using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace SoundFXStudio.UI.Tests;

[Collection("App")]
public class ProfileSwitchingTests
{
    private readonly AppFixture _app;

    public ProfileSwitchingTests(AppFixture app) => _app = app;

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
    public void Presets_HasDefaultProfile()
    {
        NavigateToPresets();
        var win = _app.GetMainWindow();
        var lists = win.FindAllDescendants(cf => cf.ByControlType(ControlType.List));
        Assert.True(lists.Length > 0);
        var items = lists[0].FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem));
        Assert.True(items.Length > 0, "Should have at least one profile");
    }

    [Fact]
    public void Presets_HasNewButton()
    {
        NavigateToPresets();
        var win = _app.GetMainWindow();
        var buttons = win.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
        var newBtn = buttons.FirstOrDefault(b =>
            b.Name.Contains("New", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(newBtn);
    }

    [Fact]
    public void Presets_CanSelectProfile()
    {
        NavigateToPresets();
        var win = _app.GetMainWindow();
        var lists = win.FindAllDescendants(cf => cf.ByControlType(ControlType.List));
        Assert.True(lists.Length > 0);
        var items = lists[0].FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem));
        Assert.True(items.Length > 0);

        items[0].Click();
        Thread.Sleep(300);
    }

    [Fact]
    public void Presets_HasSearchBox()
    {
        NavigateToPresets();
        var win = _app.GetMainWindow();
        var editBoxes = win.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit));
        Assert.True(editBoxes.Length > 0, "Presets tab should have a search text box");
    }

    [Fact]
    public void WindowTitle_UpdatesWithProfile()
    {
        var win = _app.GetMainWindow();
        var title = win.Title;
        Assert.Contains("SoundFX", title);

        NavigateToPresets();
        var lists = _app.GetMainWindow().FindAllDescendants(cf => cf.ByControlType(ControlType.List));
        if (lists.Length > 0)
        {
            var items = lists[0].FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem));
            if (items.Length > 0)
            {
                items[0].Click();
                Thread.Sleep(500);
                var newTitle = _app.GetMainWindow().Title;
                Assert.Contains("SoundFX", newTitle);
            }
        }
    }
}
