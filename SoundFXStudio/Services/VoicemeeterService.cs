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
            var regPaths = new[]
            {
                @"Software\VB-Audio\Voicemeeter",
                @"Software\Wow6432Node\VB-Audio\Voicemeeter",
                @"Software\VB-Audio\VoicemeeterPotato",
                @"Software\Wow6432Node\VB-Audio\VoicemeeterPotato",
                @"Software\VB-Audio\VBCable",          // VB-Cable (standalone)
                @"Software\Wow6432Node\VB-Audio\VBCable",
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\VB:Voicemeeter {17359A74-1236-5467}",
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\VB:Voicemeeter Banana {E431F2C7-D220-4C7B-A36B-FBAA507F6AA1}",
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\VB:Voicemeeter Potato {BDA1746F-3D44-4E5D-BC69-EC0421615921}",
            };

            foreach (var regPath in regPaths)
            {
                using var key = Registry.LocalMachine.OpenSubKey(regPath, false);
                if (key != null) return true;
            }
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
                @"C:\Program Files (x86)\VB\Voicemeeter",
                @"C:\Program Files\VB\Voicemeeter",
                @"C:\Program Files (x86)\VB-Audio\Voicemeeter",
                @"C:\Program Files\VB-Audio\Voicemeeter",
                @"C:\Program Files (x86)\VB-Audio\VoicemeeterPotato",
                @"C:\Program Files\VB-Audio\VoicemeeterPotato",
            };

            foreach (var path in paths)
            {
                if (System.IO.Directory.Exists(path))
                {
                    if (System.IO.File.Exists(System.IO.Path.Combine(path, "VoicemeeterRemote64.dll"))
                        || System.IO.File.Exists(System.IO.Path.Combine(path, "VoicemeeterRemote.dll"))
                        || System.IO.File.Exists(System.IO.Path.Combine(path, "RemoteAPI.dll")))
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
                @"C:\Program Files (x86)\VB\Voicemeeter",
                @"C:\Program Files\VB\Voicemeeter",
                @"C:\Program Files (x86)\VB-Audio\Voicemeeter",
                @"C:\Program Files\VB-Audio\Voicemeeter",
                @"C:\Program Files (x86)\VB-Audio\VoicemeeterPotato",
                @"C:\Program Files\VB-Audio\VoicemeeterPotato",
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
