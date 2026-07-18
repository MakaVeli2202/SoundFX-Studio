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
    public void MainWindow_HasCoreSidebarButtons()
    {
        var win = _app.GetMainWindow();
        var buttons = win.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));

        Assert.Contains(buttons, button => string.Equals(button.Name, "Home", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(buttons, button => string.Equals(button.Name, "Keyboard", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(buttons, button => string.Equals(button.Name, "Sound Library", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(buttons, button => string.Equals(button.Name, "Settings", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MainWindow_HasCalibrationShortcut()
    {
        var win = _app.GetMainWindow();
        var calibrationButton = win.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Button).And(cf.ByName("Calibration")));

        Assert.NotNull(calibrationButton);
    }

    [Theory]
    [InlineData("Home")]
    [InlineData("Keyboard")]
    [InlineData("Sound Library")]
    [InlineData("Settings")]
    public void MainWindow_CanFindSidebarButton(string buttonName)
    {
        var win = _app.GetMainWindow();
        var button = win.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Button).And(cf.ByName(buttonName)));

        Assert.NotNull(button);
    }
}
