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

    private static IReadOnlyList<AudioDeviceInfo> GetDevices(DataFlow flow)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var deviceStates = DeviceState.Active;

            var devices = enumerator.EnumerateAudioEndPoints(flow, deviceStates)
                .Where(device => !IsVirtualDevice(device.FriendlyName))
                .Select(device => new AudioDeviceInfo
                {
                    Id = device.ID,
                    Name = device.FriendlyName,
                    DeviceType = flow == DataFlow.Render ? "Playback" : "Recording",
                    Availability = DescribeState(device.State),
                    IsDefault = false,
                    IsDefaultCommunication = false,
                    IsInput = flow == DataFlow.Capture,
                    IsVirtual = false,
                    State = device.State
                })
                .ToList();

            if (devices.Count > 0)
            {
                devices[0].IsDefault = true;
                devices[0].IsDefaultCommunication = true;
            }

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