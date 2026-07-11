using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;

namespace SoundFXStudio.UI.Tests;

[Collection("App")]
public class AddSoundDialogTests
{
    private readonly AppFixture _app;

    public AddSoundDialogTests(AppFixture app) => _app = app;

    private Window? FindSoundDialog()
    {
        var windows = _app.App.GetAllTopLevelWindows(_app.Automation);
        return windows.FirstOrDefault(w =>
            w.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("Save"))) is not null
            && w.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("Cancel"))) is not null);
    }

    private void OpenAddSoundDialog()
    {
        var win = _app.GetMainWindow();
        var tab = win.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tab));
        Assert.NotNull(tab);
        var libraryTab = tab.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.TabItem).And(cf.ByName("Library")));
        Assert.NotNull(libraryTab);
        libraryTab.Click();
        Thread.Sleep(500);

        var addBtn = win.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Button).And(cf.ByAutomationId("LibraryAddButton")))
            ?? win.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                .FirstOrDefault(b => b.Name.Contains("Add", StringComparison.OrdinalIgnoreCase));

        if (addBtn is not null)
        {
            addBtn.Click();
            Thread.Sleep(1000);
            if (FindSoundDialog() is not null)
            {
                return;
            }
        }

        // Fallback to app shortcut if UIA button lookup is unstable.
        Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL);
        Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_N);
        Keyboard.Release(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL);
        Thread.Sleep(1000);

        Assert.NotNull(FindSoundDialog());
    }

    [Fact]
    public void AddSound_DialogOpens_WithTitle()
    {
        OpenAddSoundDialog();
        var dialog = FindSoundDialog();
        Assert.NotNull(dialog);
    }

    [Fact]
    public void AddSound_HasBrowseButton()
    {
        OpenAddSoundDialog();
        var dialog = FindSoundDialog();
        Assert.NotNull(dialog);

        var browseBtn = dialog.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Button).And(cf.ByName("Browse")));
        var browseAny = browseBtn
            ?? dialog.FindFirstDescendant(cf => cf.ByName("Browse"));
        Assert.NotNull(browseAny);
    }

    [Fact]
    public void AddSound_HasNameTextBox()
    {
        OpenAddSoundDialog();
        var dialog = FindSoundDialog();
        Assert.NotNull(dialog);

        var edits = dialog.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit));
        Assert.True(edits.Length >= 2, "Dialog should have file path + name text boxes");
    }

    [Fact]
    public void AddSound_HasCategoryComboBox()
    {
        OpenAddSoundDialog();
        var dialog = FindSoundDialog();
        Assert.NotNull(dialog);

        var combos = dialog.FindAllDescendants(cf => cf.ByControlType(ControlType.ComboBox));
        Assert.True(combos.Length >= 2, "Dialog should have keyboard key + category combos");
    }

    [Fact]
    public void AddSound_HasSaveAndCancelButtons()
    {
        OpenAddSoundDialog();
        var dialog = FindSoundDialog();
        Assert.NotNull(dialog);

        var buttons = dialog.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
        var saveBtn = buttons.FirstOrDefault(b => b.Name.Contains("Save"));
        var cancelBtn = buttons.FirstOrDefault(b => b.Name.Contains("Cancel"));
        Assert.NotNull(saveBtn);
        Assert.NotNull(cancelBtn);
    }

    [Fact]
    public void AddSound_HasFavoriteAndLoopCheckboxes()
    {
        OpenAddSoundDialog();
        var dialog = FindSoundDialog();
        Assert.NotNull(dialog);

        var checkboxes = dialog.FindAllDescendants(cf => cf.ByControlType(ControlType.CheckBox));
        Assert.True(checkboxes.Length >= 2, "Dialog should have favorite + loop checkboxes");
    }

    [Fact]
    public void AddSound_HasVolumeSlider()
    {
        OpenAddSoundDialog();
        var dialog = FindSoundDialog();
        Assert.NotNull(dialog);

        var sliders = dialog.FindAllDescendants(cf => cf.ByControlType(ControlType.Slider));
        var hasVolume = sliders.Length >= 1
            || dialog.FindFirstDescendant(cf => cf.ByName("Volume")) is not null;
        Assert.True(hasVolume, "Dialog should have a volume slider");
    }

    [Fact]
    public void AddSound_HasImageBrowseButton()
    {
        OpenAddSoundDialog();
        var dialog = FindSoundDialog();
        Assert.NotNull(dialog);

        var chooseBtn = dialog.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Button).And(cf.ByAutomationId("ImageBrowseButton")))
            ?? dialog.FindFirstDescendant(cf =>
                cf.ByControlType(ControlType.Button).And(cf.ByName("Choose")));
        var chooseAny = chooseBtn
            ?? dialog.FindFirstDescendant(cf => cf.ByName("Choose"));
        Assert.NotNull(chooseAny);
    }

    [Fact]
    public void AddSound_CancelClosesDialog()
    {
        OpenAddSoundDialog();
        var dialog = FindSoundDialog();
        Assert.NotNull(dialog);

        var cancelBtn = dialog.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Button).And(cf.ByName("Cancel")));
        Assert.NotNull(cancelBtn);
        cancelBtn.Click();
        Thread.Sleep(500);

        var stillOpen = FindSoundDialog();
        Assert.Null(stillOpen);
    }

    [Fact]
    public void AddSound_HasCloseXButton()
    {
        OpenAddSoundDialog();
        var dialog = FindSoundDialog();
        Assert.NotNull(dialog);

        var closeBtn = dialog.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Button).And(cf.ByAutomationId("Close")))
            ?? dialog.FindFirstDescendant(cf =>
                cf.ByControlType(ControlType.Button).And(cf.ByName("Close")))
            ?? dialog.FindFirstDescendant(cf =>
                cf.ByControlType(ControlType.Button).And(cf.ByName("\u2715")));
        Assert.NotNull(closeBtn);
    }
}
