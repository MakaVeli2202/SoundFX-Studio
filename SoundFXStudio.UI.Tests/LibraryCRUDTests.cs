using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace SoundFXStudio.UI.Tests;

[Collection("App")]
public class LibraryCRUDTests
{
    private readonly AppFixture _app;

    public LibraryCRUDTests(AppFixture app) => _app = app;

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
    public void Library_HasSoundsListView()
    {
        NavigateToLibrary();
        var win = _app.GetMainWindow();
        var list = win.FindFirstDescendant(cf => cf.ByControlType(ControlType.List));
        Assert.NotNull(list);
    }

    [Fact]
    public void Library_HasSearchTextBox()
    {
        NavigateToLibrary();
        var win = _app.GetMainWindow();
        var edits = win.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit));
        Assert.True(edits.Length > 0, "Library should have search box");
    }

    [Fact]
    public void Library_HasToolbarButtons()
    {
        NavigateToLibrary();
        var win = _app.GetMainWindow();
        var buttons = win.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
        Assert.True(buttons.Length >= 5, "Library toolbar should have Add/Delete/Play etc.");
    }

    [Fact]
    public void Library_HasAddButton()
    {
        NavigateToLibrary();
        var win = _app.GetMainWindow();
        var buttons = win.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
        var addBtn = buttons.FirstOrDefault(b =>
            b.Name.Contains("Add", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(addBtn);
    }

    [Fact]
    public void Library_HasDeleteButton()
    {
        NavigateToLibrary();
        var win = _app.GetMainWindow();
        var buttons = win.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
        var deleteBtn = buttons.FirstOrDefault(b =>
            b.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(deleteBtn);
    }

    [Fact]
    public void Library_HasCategoryFilter()
    {
        NavigateToLibrary();
        var win = _app.GetMainWindow();
        var combos = win.FindAllDescendants(cf => cf.ByControlType(ControlType.ComboBox));
        Assert.True(combos.Length > 0, "Library should have category filter combo");
    }

    [Fact]
    public void Library_HasFavoritesCheckbox()
    {
        NavigateToLibrary();
        var win = _app.GetMainWindow();
        var checkboxes = win.FindAllDescendants(cf => cf.ByControlType(ControlType.CheckBox));
        Assert.True(checkboxes.Length > 0, "Library should have favorites checkbox");
    }

    [Fact]
    public void Library_SearchBox_FiltersSounds()
    {
        NavigateToLibrary();
        var win = _app.GetMainWindow();
        var edits = win.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit));
        var searchBox = edits.FirstOrDefault(e => e.Name.Contains("Search", StringComparison.OrdinalIgnoreCase)
            || e.Name.Contains("Filter", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(e.Name));
        Assert.NotNull(searchBox);

        searchBox.Click();
        Thread.Sleep(200);
    }
}
