using SoundFXStudio.Models;
using SoundFXStudio.Services;
using Xunit;

namespace SoundFXStudio.Tests;

public class AudioDeviceSelectionTests
{
    private const string VmInputId = "vm-input";
    private const string VmOutputId = "vm-output";

    private static AudioDeviceInfo Device(string id, string name, bool isDefault = false, bool isDefaultCommunication = false, bool isVirtual = false)
        => new() { Id = id, Name = name, IsDefault = isDefault, IsDefaultCommunication = isDefaultCommunication, IsVirtual = isVirtual };

    [Fact]
    public void SelectionOnVoicemeeter_RestoresTheSavedWindowsDevice()
    {
        var devices = new[] { Device("headset", "Headset"), Device(VmInputId, "VoiceMeeter Input", isVirtual: true) };

        var resolved = AudioDeviceSelection.ResolveIfVoicemeeter(VmInputId, "headset", devices, VmInputId);

        Assert.Equal("headset", resolved);
    }

    [Fact]
    public void SelectionOnVoicemeeter_FallsBackToTheWindowsDefaultWhenTheSavedDeviceIsGone()
    {
        var devices = new[] { Device("usb", "USB Interface", isDefault: true), Device(VmOutputId, "VoiceMeeter Output", isVirtual: true) };

        var resolved = AudioDeviceSelection.ResolveIfVoicemeeter(VmOutputId, "removed-device", devices, VmOutputId);

        Assert.Equal("usb", resolved);
    }

    [Fact]
    public void SelectionOnVoicemeeter_WithOnlyVoicemeeterPresent_ResolvesNothing()
    {
        var devices = new[] { Device(VmInputId, "VoiceMeeter Input", isVirtual: true) };

        Assert.Null(AudioDeviceSelection.ResolveIfVoicemeeter(VmInputId, "headset", devices, VmInputId));
    }

    [Fact]
    public void SelectionOnRealHardware_IsLeftAlone()
    {
        var devices = new[] { Device("headset", "Headset"), Device("webcam", "Webcam Mic", isDefault: true) };

        Assert.Null(AudioDeviceSelection.ResolveIfVoicemeeter("headset", "webcam", devices, VmInputId));
    }

    [Fact]
    public void ResolveSystemDevice_PrefersTheWindowsCommunicationDefaultWhenNothingWasSaved()
    {
        var devices = new[]
        {
            Device("headset", "Headset", isDefault: true),
            Device("monitor", "Monitor", isDefaultCommunication: true)
        };

        var resolved = AudioDeviceSelection.ResolveSystemDeviceId(devices, null, VmInputId);

        Assert.Equal("monitor", resolved);
    }

    [Fact]
    public void ResolveSystemDevice_FallsBackToTheWindowsDefaultDevice()
    {
        var devices = new[] { Device("headset", "Headset", isDefault: true), Device("monitor", "Monitor") };

        var resolved = AudioDeviceSelection.ResolveSystemDeviceId(devices, null, VmInputId);

        Assert.Equal("headset", resolved);
    }

    [Fact]
    public void ResolveSystemDevice_WithNoWindowsDefault_FallsBackToRealHardware()
    {
        var devices = new[] { Device(VmInputId, "VoiceMeeter Input", isVirtual: true), Device("webcam", "Webcam", isVirtual: true), Device("headset", "Headset") };

        var resolved = AudioDeviceSelection.ResolveSystemDeviceId(devices, null, VmInputId);

        Assert.Equal("headset", resolved);
    }

    [Fact]
    public void ResolveSystemDevice_UsesTheSavedDeviceWhenItIsStillThere()
    {
        var devices = new[] { Device("headset", "Headset", isDefault: true), Device("monitor", "Monitor") };

        var resolved = AudioDeviceSelection.ResolveSystemDeviceId(devices, "monitor", VmInputId);

        Assert.Equal("monitor", resolved);
    }

    [Fact]
    public void ResolveSystemDevice_NeverReturnsTheVoicemeeterEndpoint()
    {
        var devices = new[] { Device(VmInputId, "VoiceMeeter Input", isDefault: true, isVirtual: true), Device("headset", "Headset") };

        var resolved = AudioDeviceSelection.ResolveSystemDeviceId(devices, VmInputId, VmInputId);

        Assert.Equal("headset", resolved);
    }

    [Fact]
    public void IsVoicemeeterEndpoint_IgnoresBlankIds()
    {
        Assert.False(AudioDeviceSelection.IsVoicemeeterEndpoint(VmInputId, null));
        Assert.False(AudioDeviceSelection.IsVoicemeeterEndpoint(null, VmInputId));
        Assert.True(AudioDeviceSelection.IsVoicemeeterEndpoint(VmInputId.ToUpperInvariant(), VmInputId));
    }
}
