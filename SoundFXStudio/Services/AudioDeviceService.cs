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

    public IReadOnlyList<AudioDeviceInfo> GetAllInputDevices()
    {
        return GetDevices(DataFlow.Capture, includeVirtual: true);
    }

    public IReadOnlyList<AudioDeviceInfo> GetAllOutputDevices()
    {
        return GetDevices(DataFlow.Render, includeVirtual: true);
    }

    public string? GetVoicemeeterInputId()
    {
        return GetVoicemeeterDeviceId(DataFlow.Render, "VoiceMeeter Input");
    }

    public string? GetVoicemeeterOutputId()
    {
        return GetVoicemeeterB1Id();
    }

    public string? GetVoicemeeterInputDeviceName()
    {
        return GetVoicemeeterDeviceName(DataFlow.Render, "VoiceMeeter Input");
    }

    public string? GetVoicemeeterB1Id()
    {
        try { return FindB1Device()?.ID; }
        catch { return null; }
    }

    public string? GetVoicemeeterB1Name()
    {
        try { return FindB1Device()?.FriendlyName; }
        catch { return null; }
    }

    private static MMDevice? FindB1Device()
    {
        using var enumerator = new MMDeviceEnumerator();
        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
        {
            var name = device.FriendlyName;
            if (!name.Contains("Voicemeeter", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!name.Contains("B1", StringComparison.OrdinalIgnoreCase))
                continue;
            if (name.Contains("Aux", StringComparison.OrdinalIgnoreCase) || name.Contains("Virtual", StringComparison.OrdinalIgnoreCase))
                continue;
            return device;
        }
        return null;
    }

    public string? GetVoicemeeterOutputDeviceName()
    {
        return GetVoicemeeterB1Name();
    }

    private static string? GetVoicemeeterDeviceId(DataFlow flow, string primaryToken)
    {
        try { return FindVoicemeeterDevice(flow, primaryToken)?.ID; }
        catch { return null; }
    }

    private static string? GetVoicemeeterDeviceName(DataFlow flow, string primaryToken)
    {
        try { return FindVoicemeeterDevice(flow, primaryToken)?.FriendlyName; }
        catch { return null; }
    }

    private static MMDevice? FindVoicemeeterDevice(DataFlow flow, string primaryToken)
    {
        using var enumerator = new MMDeviceEnumerator();
        foreach (var device in enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
        {
            var name = device.FriendlyName;
            if (!name.Contains(primaryToken, StringComparison.OrdinalIgnoreCase))
                continue;
            if (name.Contains("Aux", StringComparison.OrdinalIgnoreCase) || name.Contains("Virtual", StringComparison.OrdinalIgnoreCase))
                continue;
            return device;
        }
        return null;
    }

    public MMDevice? GetCaptureDevice(string? deviceId)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                var match = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                    .FirstOrDefault(d => string.Equals(d.ID, deviceId, StringComparison.OrdinalIgnoreCase));
                if (match is not null) return match;
            }
            return enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications)
                ?? enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
        }
        catch
        {
            return null;
        }
    }

    public string? GetDefaultDeviceId(DataFlow flow)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            return enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia)?.ID;
        }
        catch
        {
            return null;
        }
    }

    public string? GetDefaultCommunicationDeviceName(DataFlow flow)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            return enumerator.GetDefaultAudioEndpoint(flow, Role.Communications)?.FriendlyName;
        }
        catch
        {
            return null;
        }
    }

    public string? GetDefaultDeviceName(DataFlow flow)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            return enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia)?.FriendlyName;
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<AudioDeviceInfo> GetDevices(DataFlow flow, bool includeVirtual = false)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var defaultDeviceId = enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia)?.ID;
            var defaultCommunicationDeviceId = enumerator.GetDefaultAudioEndpoint(flow, Role.Communications)?.ID;
            var deviceStates = DeviceState.Active;

            var devices = enumerator.EnumerateAudioEndPoints(flow, deviceStates)
                .Where(device => includeVirtual || !IsVirtualDevice(device.FriendlyName))
                .Select(device => new AudioDeviceInfo
                {
                    Id = device.ID,
                    Name = device.FriendlyName,
                    DeviceType = flow == DataFlow.Render ? "Playback" : "Recording",
                    Availability = DescribeState(device.State),
                    IsDefault = string.Equals(device.ID, defaultDeviceId, StringComparison.OrdinalIgnoreCase),
                    IsDefaultCommunication = string.Equals(device.ID, defaultCommunicationDeviceId, StringComparison.OrdinalIgnoreCase),
                    IsInput = flow == DataFlow.Capture,
                    IsVirtual = false,
                    State = device.State
                })
                .ToList();

            return devices;
        }
        catch
        {
            return new List<AudioDeviceInfo>();
        }
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
         return name.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase)
             || name.Contains("Voicemeeter", StringComparison.OrdinalIgnoreCase)
             || name.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase)
             || name.Contains("CABLE Output", StringComparison.OrdinalIgnoreCase)
             || name.Contains("Wave Link", StringComparison.OrdinalIgnoreCase)
             || name.Contains("OBS Virtual", StringComparison.OrdinalIgnoreCase)
             || name.Contains("Stream Deck", StringComparison.OrdinalIgnoreCase);
    }

}