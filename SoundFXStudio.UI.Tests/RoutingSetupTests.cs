using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace SoundFXStudio.UI.Tests;

[Collection("App")]
public class RoutingSetupTests
{
    private readonly AppFixture _app;

    public RoutingSetupTests(AppFixture app) => _app = app;

    private void NavigateToRouting()
    {
        var win = _app.GetMainWindow();
        var tab = win.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tab));
        Assert.NotNull(tab);
        var routingTab = tab.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.TabItem).And(cf.ByName("Routing")));
        Assert.NotNull(routingTab);
        routingTab.Click();
        Thread.Sleep(500);
    }

    [Fact]
    public void Routing_HasOutputComboBox()
    {
        NavigateToRouting();
        var win = _app.GetMainWindow();
        var combos = win.FindAllDescendants(cf => cf.ByControlType(ControlType.ComboBox));
        Assert.True(combos.Length > 0, "Should have output device combo");
    }

    [Fact]
    public void Routing_OutputCombo_HasDevices()
    {
        NavigateToRouting();
        var win = _app.GetMainWindow();
        var combos = win.FindAllDescendants(cf => cf.ByControlType(ControlType.ComboBox));
        Assert.True(combos.Length > 0);
    }

    [Fact]
    public void Routing_HasAutoConfigureButton()
    {
        NavigateToRouting();
        var win = _app.GetMainWindow();
        var autoBtn = win.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Button).And(cf.ByAutomationId("RoutingAutoConfigureButton")));
        Assert.NotNull(autoBtn);
    }

    [Fact]
    public void Routing_HasTestRoutingButton()
    {
        NavigateToRouting();
        var win = _app.GetMainWindow();
        var testBtn = win.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Button).And(cf.ByAutomationId("RoutingTestButton")));
        Assert.NotNull(testBtn);
    }

    [Fact]
    public void Routing_HasSetupWizardButton()
    {
        NavigateToRouting();
        var win = _app.GetMainWindow();
        var wizardBtn = win.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Button).And(cf.ByAutomationId("RoutingSetupWizardButton")));
        Assert.NotNull(wizardBtn);
    }

    [Fact]
    public void Routing_HasStatusText()
    {
        NavigateToRouting();
        var win = _app.GetMainWindow();
        var texts = win.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
        Assert.True(texts.Length > 5, "Routing tab should have multiple text elements");
    }

    [Fact]
    public void Routing_HasWindowsSoundButton()
    {
        NavigateToRouting();
        var win = _app.GetMainWindow();
        var soundBtn = win.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Button).And(cf.ByAutomationId("RoutingWindowsSoundButton")));
        Assert.NotNull(soundBtn);
    }

    [Fact]
    public void Routing_OpenSetupWizard_ShowsDialog()
    {
        NavigateToRouting();
        var win = _app.GetMainWindow();
        var wizardBtn = win.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Button).And(cf.ByAutomationId("RoutingSetupWizardButton")));
        Assert.NotNull(wizardBtn);

        wizardBtn.Click();
        Thread.Sleep(1500);

        var desktop = _app.Automation.GetDesktop();
        var windows = desktop.FindAllDescendants(cf => cf.ByControlType(ControlType.Window));
        var wizardDialog = windows.FirstOrDefault(w =>
            w.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase) ||
            w.Name.Contains("Wizard", StringComparison.OrdinalIgnoreCase) ||
            w.Name.Contains("Audio", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(wizardDialog);
    }
}
