using System.Runtime.InteropServices;

namespace SoundFXStudio.Services;

public sealed class WindowsAudioRoutingService
{
    public string LastError { get; private set; } = string.Empty;

    public bool TrySetDefaultDevices(string outputDeviceId, string inputDeviceId)
    {
        var inputApplied = TrySetDefaultInput(inputDeviceId);
        var outputApplied = TrySetDefaultOutput(outputDeviceId);
        return outputApplied && inputApplied;
    }

    public bool TrySetDefaultOutput(string deviceId)
    {
        return string.IsNullOrWhiteSpace(deviceId) || TrySetAllRoles(deviceId);
    }

    public bool TrySetDefaultInput(string deviceId)
    {
        return string.IsNullOrWhiteSpace(deviceId) || TrySetAllRoles(deviceId);
    }

    private bool TrySetAllRoles(string deviceId)
    {
        LastError = string.Empty;
        foreach (var role in new[] { ERole.Console, ERole.Multimedia, ERole.Communications })
        {
            var (ok, hr) = TrySetDefaultEndpoint(deviceId, role);
            if (!ok)
            {
                LastError = $"role {role} failed 0x{hr:X8}";
                return false;
            }
        }
        return true;
    }

    private static (bool Ok, int Hr) TrySetDefaultEndpoint(string deviceId, ERole role)
    {
        try
        {
            var policyConfig = (IPolicyConfig)new PolicyConfigClient();
            int hr = policyConfig.SetDefaultEndpoint(deviceId, role);
            return (hr >= 0, hr);
        }
        catch (Exception ex)
        {
            return (false, ex.HResult);
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
        [PreserveSig]
        int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId, out IntPtr format);

        [PreserveSig]
        int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId, bool default_, out IntPtr format);

        [PreserveSig]
        int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId);

        [PreserveSig]
        int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr endpointFormat, IntPtr mixFormat);

        [PreserveSig]
        int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string deviceId, bool default_, out long defaultPeriod, out long minimumPeriod);

        [PreserveSig]
        int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string deviceId, long period);

        [PreserveSig]
        int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string deviceId, out int mode);

        [PreserveSig]
        int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int mode);

        [PreserveSig]
        int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string deviceId, bool store, IntPtr key, out IntPtr value);

        [PreserveSig]
        int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string deviceId, bool store, IntPtr key, IntPtr value);

        [PreserveSig]
        int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ERole role);

        [PreserveSig]
        int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string deviceId, bool visible);
    }

    private enum ERole
    {
        Console = 0,
        Multimedia = 1,
        Communications = 2
    }
}