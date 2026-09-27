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
public enum VoicemeeterCleanupResult
{
    /// <summary>No unused Voicemeeter endpoint was active, so nothing was changed and no prompt was shown.</summary>
    NothingToHide,

    /// <summary>Unused endpoints were disabled and verified.</summary>
    Completed,

    /// <summary>An endpoint could not be identified, UAC was declined, or a registry write failed.</summary>
    Failed
}

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
    /// Returns <see cref="VoicemeeterCleanupResult.Failed"/> if either used endpoint
    /// cannot be identified, UAC is declined, or any requested endpoint state fails
    /// its registry read-back.
    /// </summary>
    public static async Task<VoicemeeterCleanupResult> CleanupAsync(string? usedRenderDeviceId, string? usedCaptureDeviceId)
    {
        var usedRenderGuid = ExtractGuid(usedRenderDeviceId);
        var usedCaptureGuid = ExtractGuid(usedCaptureDeviceId);
        if (usedRenderGuid is null || usedCaptureGuid is null) return VoicemeeterCleanupResult.Failed;

        var endpointPaths = new List<string>();
        endpointPaths.AddRange(GetUnusedEndpointPaths(PmDevRender, usedRenderGuid));
        endpointPaths.AddRange(GetUnusedEndpointPaths(PmDevCapture, usedCaptureGuid));

        if (endpointPaths.Count == 0) return VoicemeeterCleanupResult.NothingToHide;

        var quotedPaths = string.Join(",", endpointPaths.Select(path => $"'{path}'"));
        var script = $@"
$ErrorActionPreference = 'Stop'
try {{
    foreach ($path in @({quotedPaths})) {{
        Set-ItemProperty -LiteralPath $path -Name DeviceState -Value 2 -ErrorAction Stop
        if ((Get-ItemProperty -LiteralPath $path -Name DeviceState -ErrorAction Stop).DeviceState -ne 2) {{ exit 2 }}
    }}
    Restart-Service -Name Audiosrv -Force -ErrorAction Stop
    exit 0
}} catch {{ exit 1 }}";
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
            if (process is null) return VoicemeeterCleanupResult.Failed;
            await process.WaitForExitAsync();
            return process.ExitCode == 0 ? VoicemeeterCleanupResult.Completed : VoicemeeterCleanupResult.Failed;
        }
        catch
        {
            // UAC declined, or elevation unavailable — leave devices as they are.
            return VoicemeeterCleanupResult.Failed;
        }
    }

    private static List<string> GetUnusedEndpointPaths(string registryRoot, string usedGuid)
    {
        var paths = new List<string>();
        using var root = Registry.LocalMachine.OpenSubKey(registryRoot);
        if (root is null) return paths;

        foreach (var guid in root.GetSubKeyNames())
        {
            if (string.Equals(guid, usedGuid, StringComparison.OrdinalIgnoreCase)) continue;

            using var device = root.OpenSubKey(guid);
            if (device is null) continue;
            if (SafeInt(device.GetValue("DeviceState")) != DeviceStateActive) continue;

            using var props = device.OpenSubKey("Properties");
            var friendly = props?.GetValue(PkeyFriendly) as string ?? string.Empty;
            if (!friendly.Contains("Voicemeeter", StringComparison.OrdinalIgnoreCase)) continue;

            paths.Add($@"HKLM:\{registryRoot}\{guid}");
        }
        return paths;
    }

    private static int? SafeInt(object? value)
    {
        return value switch
        {
            int i => i,
            uint u => (int)u,
            long l => (int)l,
            short s => s,
            byte b => b,
            string s when int.TryParse(s, out var parsed) => parsed,
            _ => null
        };
    }

    private static string? ExtractGuid(string? mmDeviceId)
    {
        if (string.IsNullOrWhiteSpace(mmDeviceId)) return null;
        var match = Regex.Match(mmDeviceId, @"\{[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\}$");
        return match.Success ? match.Value : null;
    }
}
