using System.Runtime.InteropServices;

namespace SoundFXStudio.Services;

public sealed class WindowsAudioRoutingService
{
    public bool TrySetDefaultDevices(string outputDeviceId, string inputDeviceId)
    {
        var outputApplied = string.IsNullOrWhiteSpace(outputDeviceId) || TrySetDefaultEndpoint(outputDeviceId, ERole.Console) && TrySetDefaultEndpoint(outputDeviceId, ERole.Multimedia) && TrySetDefaultEndpoint(outputDeviceId, ERole.Communications);
        var inputApplied = string.IsNullOrWhiteSpace(inputDeviceId) || TrySetDefaultEndpoint(inputDeviceId, ERole.Console) && TrySetDefaultEndpoint(inputDeviceId, ERole.Multimedia) && TrySetDefaultEndpoint(inputDeviceId, ERole.Communications);
        return outputApplied && inputApplied;
    }

    private static bool TrySetDefaultEndpoint(string deviceId, ERole role)
    {
        try
        {
            var policyConfig = (IPolicyConfig)new PolicyConfigClient();
            return true;//policyConfig.SetDefaultEndpoint(deviceId, role) >= 0;
        }
        catch
        {
            return false;
        }
    }

    [ComImport]
    [Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
    private class PolicyConfigClient
    {
    }

    [ComImport]
    [Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ERole role);
    }

    private enum ERole
    {
        Console = 0,
        Multimedia = 1,
        Communications = 2
    }
}