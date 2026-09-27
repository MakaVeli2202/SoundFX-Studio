using SoundFXStudio.Services;
using Xunit;

namespace SoundFXStudio.Tests;

/// <summary>
/// The unused-Voicemeeter-endpoint hide is re-checked on every launch rather than latched
/// behind Settings.VoicemeeterEndpointsCleaned, because Voicemeeter re-registers its
/// endpoints on driver reload and would otherwise bring every hidden channel back. These
/// tests cover only the pre-flight guards, which return before the registry is read and
/// before any UAC prompt is raised. The hide path itself is deliberately untested: on a
/// machine with Voicemeeter installed it would prompt for elevation and restart Audiosrv.
/// </summary>
public class VoicemeeterEndpointCleanupTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData(@"\\?\render#hdaudio#0#0#0#0", null)]
    [InlineData(@"not-an-mmdevice-id", "also-not-an-id")]
    public async Task CleanupAsync_WithUnresolvableDeviceIds_Fails_WithoutPrompting(string? renderId, string? captureId)
    {
        var result = await VoicemeeterEndpointCleanupService.CleanupAsync(renderId, captureId);

        // A malformed MMDevice.ID cannot be mapped to a registry key, so there is nothing
        // safe to disable. Fail rather than hide a partial set, and never prompt. This
        // returns before the registry is touched, so it is safe to run anywhere.
        Assert.Equal(VoicemeeterCleanupResult.Failed, result);
    }

    [Fact]
    public void CleanupResult_KeepsNothingToHideDistinctFromCompleted()
    {
        Assert.NotEqual(VoicemeeterCleanupResult.NothingToHide, VoicemeeterCleanupResult.Completed);
        Assert.NotEqual(VoicemeeterCleanupResult.NothingToHide, VoicemeeterCleanupResult.Failed);
        Assert.NotEqual(VoicemeeterCleanupResult.Completed, VoicemeeterCleanupResult.Failed);
    }
}
