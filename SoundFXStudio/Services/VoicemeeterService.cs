using Microsoft.Win32;

namespace SoundFXStudio.Services;

public sealed class VoicemeeterService
{
    /// <summary>
    /// Checks if Voicemeeter is installed on the system (multiple detection methods)
    /// </summary>
    public static bool IsVoicemeeterInstalled()
    {
        // Try registry check first (most reliable)
        if (CheckVoicemeeterRegistry())
            return true;

        // Try checking common installation paths
        if (CheckVoicemeeterInstallPath())
            return true;

        // Try DLL load attempt as final fallback
        return TryLoadVoicemeeterDll();
    }

    /// <summary>
    /// Checks Windows registry for Voicemeeter installation
    /// </summary>
    private static bool CheckVoicemeeterRegistry()
    {
        try
        {
            // Check HKLM for Voicemeeter registry key
            using var key = Registry.LocalMachine.OpenSubKey(@"Software\VB-Audio\Voicemeeter", false);
            if (key != null)
                return true;

            // Also check for 64-bit installation
            using var key64 = Registry.LocalMachine.OpenSubKey(@"Software\Wow6432Node\VB-Audio\Voicemeeter", false);
            if (key64 != null)
                return true;

            // Check for newer Voicemeeter Potato
            using var keyPotato = Registry.LocalMachine.OpenSubKey(@"Software\VB-Audio\VoicemeeterPotato", false);
            if (keyPotato != null)
                return true;
        }
        catch
        {
            // Ignore exceptions
        }

        return false;
    }

    /// <summary>
    /// Checks common Voicemeeter installation directories
    /// </summary>
    private static bool CheckVoicemeeterInstallPath()
    {
        try
        {
            var paths = new[]
            {
                @"C:\Program Files\VB-Audio\Voicemeeter",
                @"C:\Program Files (x86)\VB-Audio\Voicemeeter",
                @"C:\Program Files\VB-Audio\VoicemeeterPotato",
                @"C:\Program Files (x86)\VB-Audio\VoicemeeterPotato",
            };

            foreach (var path in paths)
            {
                if (System.IO.Directory.Exists(path))
                {
                    // Check for key DLL files
                    if (System.IO.File.Exists(System.IO.Path.Combine(path, "RemoteAPI.dll")))
                        return true;
                }
            }
        }
        catch
        {
            // Ignore exceptions
        }

        return false;
    }

    /// <summary>
    /// Attempts to load Voicemeeter RemoteAPI DLL (graceful failure if not installed)
    /// </summary>
    private static bool TryLoadVoicemeeterDll()
    {
        try
        {
            var dll = System.Runtime.InteropServices.NativeLibrary.Load("RemoteAPI.dll");
            return dll != IntPtr.Zero;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the Voicemeeter installation path from registry
    /// </summary>
    public static string? GetVoicemeeterInstallPath()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"Software\VB-Audio\Voicemeeter", false);
            var path = key?.GetValue("InstallPath") as string;
            if (!string.IsNullOrEmpty(path))
                return path;

            // Try Voicemeeter Potato
            using var keyPotato = Registry.LocalMachine.OpenSubKey(@"Software\VB-Audio\VoicemeeterPotato", false);
            var potatoPath = keyPotato?.GetValue("InstallPath") as string;
            if (!string.IsNullOrEmpty(potatoPath))
                return potatoPath;

            // Fallback to common paths
            var commonPaths = new[]
            {
                @"C:\Program Files\VB-Audio\Voicemeeter",
                @"C:\Program Files (x86)\VB-Audio\Voicemeeter",
                @"C:\Program Files\VB-Audio\VoicemeeterPotato",
                @"C:\Program Files (x86)\VB-Audio\VoicemeeterPotato",
            };

            foreach (var commonPath in commonPaths)
            {
                if (System.IO.Directory.Exists(commonPath))
                    return commonPath;
            }
        }
        catch
        {
            // Ignore exceptions
        }

        return null;
    }
}
