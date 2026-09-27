using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace SoundFXStudio.Services;

/// <summary>
/// One-time cleanup: disables every Voicemeeter endpoint SoundFX Studio does
/// not route through, so Windows' Sound flyout stops listing Input/Aux/B2/B3
/// entries nobody uses. Same registry mechanism (DeviceState) ArtTuneStackService
/// already uses for its own endpoints. Requires one elevation prompt (UAC).
/// </summary>
/// <remarks>
/// Deliberately does NOT rename the two endpoints actually in use. Renaming
/// changes the WASAPI friendly name, but every lookup in this codebase
/// (AudioDeviceService.FindVoicemeeterDevice/FindB1Device, and the legacy
/// WaveOut/MME name match in ResolveVoicemeeterInputWaveOutIndex) identifies
/// them BY that name — renaming would break re-detection on the next run and
/// could silently misroute playback. Hiding the unused ones carries none of
/// that risk since nothing in the app ever looks them up.
/// </remarks>
public static class VoicemeeterEndpointCleanupService
{
    private const string PkeyFriendly = "{a45c254e-df1c-4efd-8020-67d146a850e0},2";
    private const string PmDevRender = @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render";
    private const string PmDevCapture = @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture";
    private const int DeviceStateActive = 1;

    /// <summary>
    /// Disables every active Voicemeeter endpoint except the given used
    /// render/capture ones. IDs are NAudio MMDevice.ID strings (the trailing
    /// {guid} is extracted to match the registry key name).
    /// Returns true if nothing needed elevating, or the elevated import
    /// succeeded; false if the user declined UAC or it failed.
    /// </summary>
    public static async Task<bool> CleanupAsync(string? usedRenderDeviceId, string? usedCaptureDeviceId)
    {
        var usedRenderGuid = ExtractGuid(usedRenderDeviceId);
        var usedCaptureGuid = ExtractGuid(usedCaptureDeviceId);

        var regBlocks = new List<string>();
        regBlocks.AddRange(BuildDisableBlocks(PmDevRender, usedRenderGuid));
        regBlocks.AddRange(BuildDisableBlocks(PmDevCapture, usedCaptureGuid));

        if (regBlocks.Count == 0) return true; // nothing unused to hide

        var regFile = Path.Combine(Path.GetTempPath(), $"sfx-vm-cleanup-{Guid.NewGuid():N}.reg");
        var regContent = "Windows Registry Editor Version 5.00\r\n\r\n" + string.Join("\r\n\r\n", regBlocks);
        await File.WriteAllTextAsync(regFile, regContent, Encoding.ASCII);

        var script =
            $"regedit /s \"{regFile}\"; " +
            "Restart-Service -Name Audiosrv -Force -ErrorAction SilentlyContinue; " +
            "Start-Sleep -Milliseconds 500; " +
            $"Remove-Item \"{regFile}\" -Force -ErrorAction SilentlyContinue";
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

        var psi = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"),
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encoded}"
        };

        try
        {
            using var process = Process.Start(psi);
            if (process is null) return false;
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            // UAC declined, or elevation unavailable — leave devices as they are.
            return false;
        }
    }

    private static List<string> BuildDisableBlocks(string registryRoot, string? usedGuid)
    {
        var blocks = new List<string>();
        using var root = Registry.LocalMachine.OpenSubKey(registryRoot);
        if (root is null) return blocks;

        foreach (var guid in root.GetSubKeyNames())
        {
            if (string.Equals(guid, usedGuid, StringComparison.OrdinalIgnoreCase)) continue;

            using var device = root.OpenSubKey(guid);
            if (device is null) continue;
            if (SafeInt(device.GetValue("DeviceState")) != DeviceStateActive) continue;

            using var props = device.OpenSubKey("Properties");
            var friendly = props?.GetValue(PkeyFriendly) as string ?? string.Empty;
            if (!friendly.Contains("Voicemeeter", StringComparison.OrdinalIgnoreCase)) continue;

            blocks.Add($"[HKEY_LOCAL_MACHINE\\{registryRoot}\\{guid}]\r\n\"DeviceState\"=dword:00000002");
        }
        return blocks;
    }

    private static int? SafeInt(object? value)
    {
        try { return value is null ? null : Convert.ToInt32(value); }
        catch { return null; }
    }

    private static string? ExtractGuid(string? mmDeviceId)
    {
        if (string.IsNullOrWhiteSpace(mmDeviceId)) return null;
        var match = Regex.Match(mmDeviceId, @"\{[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\}$");
        return match.Success ? match.Value : null;
    }
}
