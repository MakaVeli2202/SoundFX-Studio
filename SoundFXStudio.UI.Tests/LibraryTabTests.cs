using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace SoundFXStudio.UI.Tests;

[Collection("App")]
public class LibraryTabTests
{
    private readonly AppFixture _app;

    public LibraryTabTests(AppFixture app) => _app = app;

    private void NavigateToLibrary()
    {
        var win = _app.GetMainWindow();
        var tab = win.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tab));
        Assert.NotNull(tab);
        var libraryTab = tab.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.TabItem).And(cf.ByName("Library")));
        Assert.NotNull(libraryTab);
        libraryTab.Click();
        Thread.Sleep(500);
    }

    [Fact]
    public void LibraryTab_HasSoundsListView()
    {
        NavigateToLibrary();
        var win = _app.GetMainWindow();
        var listView = win.FindFirstDescendant(cf => cf.ByControlType(ControlType.List));
        Assert.NotNull(listView);
    }

    [Fact]
    public void LibraryTab_HasSearchBox()
    {
        NavigateToLibrary();
        var win = _app.GetMainWindow();
        var textBoxes = win.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit));
        Assert.True(textBoxes.Length > 0, "Library tab should have at least one text box (search)");
    }

    [Fact]
    public void LibraryTab_HasAddButton()
    {
        NavigateToLibrary();
        var win = _app.GetMainWindow();
        var buttons = win.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
        var addButton = buttons.FirstOrDefault(b =>
            b.Name.Contains("Add", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(addButton);
    }

    [Fact]
    public void LibraryTab_HasDeleteButton()
    {
        NavigateToLibrary();
        var win = _app.GetMainWindow();
        var buttons = win.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
        var deleteButton = buttons.FirstOrDefault(b =>
            b.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(deleteButton);
    }
}
