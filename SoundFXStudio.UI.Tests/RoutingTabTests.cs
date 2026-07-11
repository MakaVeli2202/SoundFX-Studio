using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace SoundFXStudio.UI.Tests;

[Collection("App")]
public class RoutingTabTests
{
    private readonly AppFixture _app;

    public RoutingTabTests(AppFixture app) => _app = app;

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
    public void RoutingTab_HasOutputDeviceComboBox()
    {
        NavigateToRouting();
        var win = _app.GetMainWindow();
        var combos = win.FindAllDescendants(cf => cf.ByControlType(ControlType.ComboBox));
        Assert.True(combos.Length > 0, "Routing tab should have device combo boxes");
    }

    [Fact]
    public void RoutingTab_HasSetupWizardButton()
    {
        NavigateToRouting();
        var win = _app.GetMainWindow();
        var buttons = win.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
        var wizardBtn = buttons.FirstOrDefault(b =>
            b.Name.Contains("Wizard", StringComparison.OrdinalIgnoreCase) ||
            b.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(wizardBtn);
    }

    [Fact]
    public void RoutingTab_HasStatusCards()
    {
        NavigateToRouting();
        var win = _app.GetMainWindow();
        var textBlocks = win.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
        var statusTexts = textBlocks.Where(t =>
            t.Name.Contains("Output", StringComparison.OrdinalIgnoreCase) ||
            t.Name.Contains("Input", StringComparison.OrdinalIgnoreCase) ||
            t.Name.Contains("Preset", StringComparison.OrdinalIgnoreCase));
        Assert.True(statusTexts.Any(), "Routing tab should show status info");
    }
}
