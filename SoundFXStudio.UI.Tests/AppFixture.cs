using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using System.Diagnostics;
using System.IO;

namespace SoundFXStudio.UI.Tests;

public class AppFixture : IDisposable
{
    public Application App { get; private set; } = null!;
    public UIA3Automation Automation { get; private set; } = null!;

    public AppFixture()
    {
        var binDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
                "SoundFXStudio", "bin", "Debug", "net8.0-windows"));
        var appPath = Path.Combine(binDir, "SoundFXStudio.exe");

        if (!File.Exists(appPath))
            throw new FileNotFoundException($"App not found at {appPath}. Build first.");

        Automation = new UIA3Automation();
        App = Application.Launch(appPath);
        Thread.Sleep(2000);
        DismissSetupWizardIfNeeded();
    }

    private void DismissSetupWizardIfNeeded()
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var windows = App.GetAllTopLevelWindows(Automation);

                foreach (var w in windows)
                {
                    var finishBtn = w.FindFirstDescendant(cf =>
                        cf.ByControlType(ControlType.Button).And(cf.ByName("Finish")));
                    if (finishBtn != null)
                    {
                        finishBtn.Click();
                        Thread.Sleep(1500);
                        return;
                    }

                    var tabControl = w.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tab));
                    if (tabControl != null)
                        return;
                }
            }
            catch { }
            Thread.Sleep(500);
        }
    }

    public Window GetMainWindow()
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var appMainWindow = App.GetMainWindow(Automation);
                if (appMainWindow != null)
                {
                    var hasTabs = appMainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tab)) != null;
                    if (hasTabs)
                    {
                        return appMainWindow;
                    }
                }

                var windows = App.GetAllTopLevelWindows(Automation);

                foreach (var w in windows)
                {
                    var hasTabs = w.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tab)) != null;
                    if (hasTabs && w is Window win)
                        return win;
                }
            }
            catch { }
            Thread.Sleep(500);
        }

        throw new TimeoutException("Could not find SoundFX Studio main window.");
    }

    public void Dispose()
    {
        try { App?.Close(); } catch { }
        Thread.Sleep(500);
        try { App?.Dispose(); } catch { }
        Automation?.Dispose();
    }
}

[CollectionDefinition("App")]
public class AppCollection : ICollectionFixture<AppFixture> { }
