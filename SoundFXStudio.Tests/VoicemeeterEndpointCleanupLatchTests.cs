using SoundFXStudio.Models;
using SoundFXStudio.Services;
using Xunit;

namespace SoundFXStudio.Tests;

public class VoicemeeterEndpointCleanupLatchTests
{
    [Fact]
    public void VoicemeeterEndpointsCleaned_DefaultsToFalse_SoFirstLaunchStillCleansUp()
    {
        var settings = new AppSettings();

        Assert.False(settings.VoicemeeterEndpointsCleaned);
    }

    [Fact]
    public void VoicemeeterEndpointsCleaned_RoundTrips_SoTheLatchSurvivesRestart()
    {
        var settings = new AppSettings();

        settings.VoicemeeterEndpointsCleaned = true;

        Assert.True(settings.VoicemeeterEndpointsCleaned);
    }

    [Theory]
    [InlineData(VoicemeeterCleanupResult.Completed, true)]
    [InlineData(VoicemeeterCleanupResult.NothingToHide, true)]
    [InlineData(VoicemeeterCleanupResult.Failed, false)]
    public void CleanupResult_LatchesFlag_ForEveryOutcomeExceptFailure(
        VoicemeeterCleanupResult result,
        bool expectedToLatch)
    {
        var settings = new AppSettings();

        if (result != VoicemeeterCleanupResult.Failed)
            settings.VoicemeeterEndpointsCleaned = true;

        Assert.Equal(expectedToLatch, settings.VoicemeeterEndpointsCleaned);
    }

    [Fact]
    public void CleanupResult_ReportsFailure_OnlyWhenTheUserDeclinesElevation()
    {
        Assert.Equal(3, Enum.GetValues<VoicemeeterCleanupResult>().Length);
        Assert.NotEqual(VoicemeeterCleanupResult.Failed, VoicemeeterCleanupResult.Completed);
        Assert.NotEqual(VoicemeeterCleanupResult.Failed, VoicemeeterCleanupResult.NothingToHide);
    }
}
