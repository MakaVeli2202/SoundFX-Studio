using System.Text.RegularExpressions;
using Xunit;

namespace SoundFXStudio.Tests;

/// <summary>
/// Voicemeeter setup is routing-only. It must not write a Voicemeeter endpoint into the app
/// INPUT/OUTPUT settings, because that moves the pickers off the devices the user has
/// selected in Windows onto the virtual bus. The behaviour lives in WPF windows that need
/// a UI session, so this guards the call sites instead.
/// </summary>
public class AudioDeviceWiringTests
{
    private static readonly string[] RoutingCallSites =
    {
        "SoundFXStudio/ViewModels/MainViewModel.cs",
        "SoundFXStudio/Views/MainWindow.xaml.cs",
        "SoundFXStudio/Views/Dialogs/SetupWizardWindow.xaml.cs"
    };

    [Fact]
    public void RoutingCode_NeverWritesAVoicemeeterEndpointIntoTheAppDeviceSettings()
    {
        var forbidden = new Regex(
            @"Settings\.(Input|Output|Microphone|Playback)DeviceId\s*=\s*vm(Input|Output)Id",
            RegexOptions.IgnoreCase);

        var offenders = new List<string>();
        foreach (var relative in RoutingCallSites)
        {
            var source = File.ReadAllText(Path.Combine(FindRepoRoot(), relative));
            if (forbidden.IsMatch(source))
            {
                offenders.Add(relative);
            }
        }

        Assert.Empty(offenders);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SoundFXStudio.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate SoundFXStudio.sln above " + AppContext.BaseDirectory);
    }
}
