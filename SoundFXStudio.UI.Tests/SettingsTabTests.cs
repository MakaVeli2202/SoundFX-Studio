using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace SoundFXStudio.UI.Tests;

[Collection("App")]
public class SettingsTabTests
{
    private readonly AppFixture _app;

    public SettingsTabTests(AppFixture app) => _app = app;

    private void NavigateToSettings()
    {
        var win = _app.GetMainWindow();
        var tab = win.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tab));
        Assert.NotNull(tab);
        var settingsTab = tab.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.TabItem).And(cf.ByName("Settings")));
        Assert.NotNull(settingsTab);
        settingsTab.Click();
        Thread.Sleep(500);
    }

    [Fact]
    public void SettingsTab_HasGeneralSection()
    {
        NavigateToSettings();
        var win = _app.GetMainWindow();
        var general = win.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Text).And(cf.ByAutomationId("SettingsGeneralHeader")))
            ?? win.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
                .FirstOrDefault(t => t.Name.Contains("General", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(general);
    }

    [Fact]
    public void SettingsTab_HasAudioDevicesSection()
    {
        NavigateToSettings();
        var win = _app.GetMainWindow();
        var texts = win.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
        var audioText = texts.FirstOrDefault(t =>
            t.Name.Contains("Audio", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(audioText);
    }

    [Fact]
    public void SettingsTab_HasDeviceComboBoxes()
    {
        NavigateToSettings();
        var win = _app.GetMainWindow();
        var combos = win.FindAllDescendants(cf => cf.ByControlType(ControlType.ComboBox));
        Assert.True(combos.Length >= 2, "Settings should have at least output + input device combos");
    }
}
