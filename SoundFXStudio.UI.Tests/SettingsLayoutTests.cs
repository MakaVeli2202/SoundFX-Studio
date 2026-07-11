using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace SoundFXStudio.UI.Tests;

[Collection("App")]
public class SettingsLayoutTests
{
    private readonly AppFixture _app;

    public SettingsLayoutTests(AppFixture app) => _app = app;

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
    public void Settings_HasScrollableContent()
    {
        NavigateToSettings();
        var win = _app.GetMainWindow();
        var texts = win.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
        Assert.True(texts.Length > 5, "Settings tab should have many text elements (scrollable content)");
    }

    [Fact]
    public void Settings_HasGeneralSection()
    {
        NavigateToSettings();
        var win = _app.GetMainWindow();
        var texts = win.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
        var general = texts.FirstOrDefault(t =>
            t.Name.Contains("General", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(general);
    }

    [Fact]
    public void Settings_HasAppearanceSection()
    {
        NavigateToSettings();
        var win = _app.GetMainWindow();
        var texts = win.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
        var appearance = texts.FirstOrDefault(t =>
            t.Name.Contains("Appearance", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(appearance);
    }

    [Fact]
    public void Settings_HasAudioDevicesSection()
    {
        NavigateToSettings();
        var win = _app.GetMainWindow();
        var texts = win.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
        var audio = texts.FirstOrDefault(t =>
            t.Name.Contains("Audio", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(audio);
    }

    [Fact]
    public void Settings_HasOutputDeviceComboBox()
    {
        NavigateToSettings();
        var win = _app.GetMainWindow();
        var combos = win.FindAllDescendants(cf => cf.ByControlType(ControlType.ComboBox));
        Assert.True(combos.Length >= 2, "Settings should have output + input device combos");
    }

    [Fact]
    public void Settings_HasCheckBoxes()
    {
        NavigateToSettings();
        var win = _app.GetMainWindow();
        var checkboxes = win.FindAllDescendants(cf => cf.ByControlType(ControlType.CheckBox));
        Assert.True(checkboxes.Length > 0, "Settings should have toggle checkboxes");
    }

    [Fact]
    public void Settings_HasKeyboardShortcutsSection()
    {
        NavigateToSettings();
        var win = _app.GetMainWindow();
        var texts = win.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
        var shortcuts = texts.FirstOrDefault(t =>
            t.Name.Contains("Shortcut", StringComparison.OrdinalIgnoreCase) ||
            t.Name.Contains("Hotkey", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(shortcuts);
    }

    [Fact]
    public void Settings_HasSetupWizardButton()
    {
        NavigateToSettings();
        var win = _app.GetMainWindow();
        var buttons = win.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
        var wizardBtn = buttons.FirstOrDefault(b =>
            b.Name.Contains("Wizard", StringComparison.OrdinalIgnoreCase) ||
            b.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(wizardBtn);
    }
}
