using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace SoundFXStudio.UI.Tests;

[Collection("App")]
public class MainWindowTests
{
    private readonly AppFixture _app;

    public MainWindowTests(AppFixture app) => _app = app;

    [Fact]
    public void App_ShowsMainWindow_WithTitle()
    {
        var win = _app.GetMainWindow();
        Assert.NotNull(win);
        Assert.Contains("SoundFX", win.Title);
    }

    [Fact]
    public void MainWindow_HasTabControl_With6Tabs()
    {
        var win = _app.GetMainWindow();
        var tab = win.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tab));
        Assert.NotNull(tab);

        var tabs = tab.FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem));
        Assert.Equal(6, tabs.Length);
    }

    [Fact]
    public void MainWindow_TabHeaders_ContainExpectedNames()
    {
        var win = _app.GetMainWindow();
        var tab = win.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tab));
        Assert.NotNull(tab);
        var tabs = tab.FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem));

        var headers = tabs.Select(t => t.Name).ToList();
        Assert.Contains("Keyboard", headers);
        Assert.Contains("Routing", headers);
        Assert.Contains("Library", headers);
        Assert.Contains("Settings", headers);
        Assert.Contains("Presets", headers);
        Assert.Contains("Statistics", headers);
    }

    [Theory]
    [InlineData("Keyboard")]
    [InlineData("Routing")]
    [InlineData("Library")]
    [InlineData("Settings")]
    [InlineData("Presets")]
    [InlineData("Statistics")]
    public void MainWindow_CanSwitchToTab(string tabName)
    {
        var win = _app.GetMainWindow();
        var tab = win.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tab));
        Assert.NotNull(tab);
        var tabItem = tab.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.TabItem).And(cf.ByName(tabName)));
        Assert.NotNull(tabItem);

        tabItem.Click();
        Thread.Sleep(300);

        Assert.False(tabItem.IsOffscreen, $"Tab '{tabName}' should be visible after clicking");
    }
}
