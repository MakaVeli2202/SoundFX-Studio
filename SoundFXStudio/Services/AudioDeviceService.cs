using NAudio.CoreAudioApi;
using SoundFXStudio.Models;

namespace SoundFXStudio.Services;

public sealed class AudioDeviceService
{
    public IReadOnlyList<AudioDeviceInfo> GetOutputDevices()
    {
        return GetDevices(DataFlow.Render);
    }

    public IReadOnlyList<AudioDeviceInfo> GetInputDevices()
    {
        return GetDevices(DataFlow.Capture);
    }

    public string? GetDefaultCommunicationDeviceName(DataFlow flow)
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator.GetDefaultAudioEndpoint(flow, Role.Communications)?.FriendlyName;
    }

    public bool IsVBCableInstalled(IEnumerable<AudioDeviceInfo> devices)
        => devices.Any(device => IsVBCableDevice(device.Name));

    public bool IsVBCableDevice(string name)
    {
        var normalized = name.Trim();
        return normalized.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("CABLE Output", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("Voicemeeter", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("Wave Link", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<AudioDeviceInfo> GetDevices(DataFlow flow)
    {
        using var enumerator = new MMDeviceEnumerator();
        var defaultDeviceId = enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia)?.ID;
        var defaultCommunicationDeviceId = enumerator.GetDefaultAudioEndpoint(flow, Role.Communications)?.ID;

        var deviceStates = DeviceState.Active | DeviceState.Disabled | DeviceState.NotPresent | DeviceState.Unplugged;

        return enumerator.EnumerateAudioEndPoints(flow, deviceStates)
            .Select(device => new AudioDeviceInfo
            {
                Id = device.ID,
                Name = device.FriendlyName,
                DeviceType = flow == DataFlow.Render ? "Playback" : "Recording",
                Availability = DescribeState(device.State),
                IsDefault = string.Equals(device.ID, defaultDeviceId, StringComparison.OrdinalIgnoreCase),
                IsDefaultCommunication = string.Equals(device.ID, defaultCommunicationDeviceId, StringComparison.OrdinalIgnoreCase),
                IsInput = flow == DataFlow.Capture,
                IsVirtual = IsVirtualDevice(device.FriendlyName),
                State = device.State
            })
            .ToList();
    }

    private static string DescribeState(DeviceState state)
        => state switch
        {
            DeviceState.Active => "Active",
            DeviceState.Disabled => "Disabled",
            DeviceState.NotPresent => "Not Present",
            DeviceState.Unplugged => "Unplugged",
            _ => state.ToString()
        };

    private static bool IsVirtualDevice(string name)
    {
        return name.Contains("CABLE", StringComparison.OrdinalIgnoreCase)
               || name.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Voicemeeter", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Wave Link", StringComparison.OrdinalIgnoreCase)
               || name.Contains("OBS", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Virtual", StringComparison.OrdinalIgnoreCase);
    }
}