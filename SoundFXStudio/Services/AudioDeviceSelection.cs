using SoundFXStudio.Models;

namespace SoundFXStudio.Services;

/// <summary>
/// The app's INPUT/OUTPUT pickers must always show the devices Windows has selected, never
/// a Voicemeeter endpoint. Voicemeeter setup only applies routing: the soundboard and voice
/// changer resolve Voicemeeter Input themselves whenever Voicemeeter is running, so the
/// pickers never needed to move onto the virtual bus. Older builds wrote VoiceMeeter
/// Input / Out B1 into the settings while configuring, which parked the pickers on the
/// virtual devices and silently dropped the user's hardware.
/// </summary>
public static class AudioDeviceSelection
{
    public static bool IsVoicemeeterEndpoint(string? deviceId, string? voicemeeterId)
        => !string.IsNullOrWhiteSpace(deviceId)
           && !string.IsNullOrWhiteSpace(voicemeeterId)
           && string.Equals(deviceId, voicemeeterId, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Resolves the device the app should show for a flow: the id Windows currently has
    /// selected, then the device saved before Voicemeeter took the bus, then Windows'
    /// default. A Voicemeeter endpoint is never returned.
    /// </summary>
    public static string? ResolveSystemDeviceId(IEnumerable<AudioDeviceInfo> devices, string? savedId, string? voicemeeterId)
    {
        var list = devices.Where(d => !IsVoicemeeterEndpoint(d.Id, voicemeeterId)).ToList();
        if (list.Count == 0)
        {
            return null;
        }

        if (!IsVoicemeeterEndpoint(savedId, voicemeeterId)
            && list.Any(d => string.Equals(d.Id, savedId, StringComparison.OrdinalIgnoreCase)))
        {
            return savedId;
        }

        var byDefault = list.FirstOrDefault(d => d.IsDefaultCommunication) ?? list.FirstOrDefault(d => d.IsDefault);
        if (byDefault is not null)
        {
            return byDefault.Id;
        }

        return list.FirstOrDefault(d => !d.IsVirtual)?.Id ?? list[0].Id;
    }

    /// <summary>
    /// Returns the device to put back into the app pickers when the saved selection is a
    /// Voicemeeter endpoint, or null when the selection is a genuine user choice that has
    /// to be left alone.
    /// </summary>
    public static string? ResolveIfVoicemeeter(
        string? selectedId,
        string? savedId,
        IEnumerable<AudioDeviceInfo> devices,
        string? voicemeeterId)
    {
        return IsVoicemeeterEndpoint(selectedId, voicemeeterId)
            ? ResolveSystemDeviceId(devices, savedId, voicemeeterId)
            : null;
    }
}
