using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using NAudio.CoreAudioApi;

namespace SoundFXStudio.Services.ArtTune;

/// <summary>
/// Describes the installed state of the ArtTune audio stack (VB-CABLE,
/// Voicemeeter, ReaPlugs, Equalizer APO, HeSuVi, LEQ Control Panel and the
/// installed ArtTuneDB library). Read-only detection, no elevation needed.
/// </summary>
public sealed class ArtTuneStackState
{
    public bool EqualizerApoInstalled { get; init; }
    public string EqualizerApoPath { get; init; } = string.Empty;
    public bool HeSuViInstalled { get; init; }
    public bool ReaPlugsInstalled { get; init; }
    public bool VoicemeeterInstalled { get; init; }
    public bool VbCableDetected { get; init; }
    public bool LeqInstalled { get; init; }
    public string LibraryVersion { get; init; } = string.Empty;
    public string? Render8Guid { get; init; }
    public string? Render16Guid { get; init; }
    public string? CaptureGuid { get; init; }
    public string Render8Name { get; init; } = string.Empty;
    public string Render16Name { get; init; } = string.Empty;
    public string CaptureName { get; init; } = string.Empty;

    public bool LibraryInstalled => LibraryVersion.Length > 0;
    public bool EndpointsRenamed =>
        string.Equals(Render8Name, "Art Tune", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Render16Name, "Art Tune +", StringComparison.OrdinalIgnoreCase);

    public bool AllCoreInstalled => EqualizerApoInstalled && HeSuViInstalled &&
                                    ReaPlugsInstalled && VbCableDetected &&
                                    LeqInstalled && LibraryInstalled;

    public IReadOnlyList<string> MissingComponents
    {
        get
        {
            var missing = new List<string>();
            if (!VbCableDetected) missing.Add("VB-CABLE");
            if (!VoicemeeterInstalled) missing.Add("Voicemeeter");
            if (!ReaPlugsInstalled) missing.Add("ReaPlugs");
            if (!EqualizerApoInstalled) missing.Add("Equalizer APO");
            if (!HeSuViInstalled) missing.Add("HeSuVi");
            if (!LeqInstalled) missing.Add("LEQ Control Panel");
            if (!LibraryInstalled) missing.Add("ArtTuneDB library");
            return missing;
        }
    }
}

/// <summary>
/// One-button engine for the ArtTune stack. Detects installed components in
/// pure C# (read-only) and drives a bundled, elevated PowerShell port of the
/// ArtIsWar/ArtTuneDB Install-ArtTune.ps1 for install/apply actions. Output is
/// streamed line-by-line via a log file the elevated process tee's into.
/// </summary>
public sealed class ArtTuneStackService
{
    public const string ArtTune8Name = "Art Tune";
    public const string ArtTune16Name = "Art Tune +";
    public const string UnifiedOutputName = "Art Tune Unified Output";
    public const string GuidedUrl = "https://artiswar.io/tools/ArtTuneGuided";

    private const string PkeyFriendly = "{a45c254e-df1c-4efd-8020-67d146a850e0},2";
    private const string PkeyDesc = "{b3f8fa53-0004-438e-9003-51a46e139bfc},6";
    private const string PkeyFormFactor = "{1da5d803-d492-4edd-8c23-e0c0ffee7f0e},0";
    private const string PmDevRender = @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render";
    private const string PmDevCapture = @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture";
    private const int DeviceStateActive = 1;

    private static readonly Regex SafeArgRegex = new(@"^[A-Za-z0-9_\-\.\\/]+$", RegexOptions.Compiled);

    /// <summary>Relative script path bundled into the output folder.</summary>
    public string ScriptPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "Assets", "ArtTune", "ArtTune-OneClick.ps1");

    public static string EqualizerApoRoot
    {
        get
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\EqualizerAPO");
                var path = key?.GetValue("InstallPath") as string;
                if (!string.IsNullOrWhiteSpace(path)) return path.Trim();
            }
            catch { /* fall through */ }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "EqualizerAPO");
        }
    }

    /// <summary>Read-only stack state detection. Never elevates.</summary>
    public static ArtTuneStackState Detect()
    {
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var eapoPath = EqualizerApoRoot;
        var libraryVersion = string.Empty;
        try
        {
            var versionFile = Path.Combine(pf, "EqualizerAPO", "config", "ArtTuneDB", "library", "version.txt");
            if (File.Exists(versionFile)) libraryVersion = File.ReadAllText(versionFile).Trim();
        }
        catch { /* best effort */ }

        var endpoints = ReadEndpoints();

        return new ArtTuneStackState
        {
            EqualizerApoInstalled = Directory.Exists(Path.Combine(eapoPath, "config")),
            EqualizerApoPath = eapoPath,
            HeSuViInstalled = File.Exists(Path.Combine(pf, "EqualizerAPO", "config", "HeSuVi", "hesuvi.txt")),
            ReaPlugsInstalled = Directory.Exists(Path.Combine(pf, "VSTPlugins", "ReaPlugs")),
            VoicemeeterInstalled = VoicemeeterStandardPresent(),
            VbCableDetected = endpoints.Render8 != null || endpoints.Render16 != null || endpoints.Capture != null,
            LeqInstalled = File.Exists(Path.Combine(local, "Programs", "LEQControlPanel", "LEQControlPanel.exe")),
            LibraryVersion = libraryVersion,
            Render8Guid = endpoints.Render8,
            Render16Guid = endpoints.Render16,
            CaptureGuid = endpoints.Capture,
            Render8Name = endpoints.Render8Name ?? string.Empty,
            Render16Name = endpoints.Render16Name ?? string.Empty,
            CaptureName = endpoints.CaptureName ?? string.Empty
        };
    }

    public static bool VoicemeeterStandardPresent()
    {
        var folder = VoicemeeterFolder();
        return File.Exists(Path.Combine(folder, "voicemeeter.exe"));
    }

    private static string VoicemeeterFolder()
    {
        var keyPath = @"VB:Voicemeeter {17359A74-1236-5467}";
        foreach (var root in new[] { Registry.LocalMachine, Registry.LocalMachine })
        {
            foreach (var subKey in new[]
                     {
                         Path.Combine(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", keyPath),
                         Path.Combine(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", keyPath)
                     })
            {
                using var key = root.OpenSubKey(subKey);
                if (key is null) continue;
                var install = key.GetValue("InstallLocation") as string;
                if (!string.IsNullOrWhiteSpace(install))
                {
                    install = install.Trim().Trim('"');
                    if (Directory.Exists(install)) return install;
                }
                var uninstall = key.GetValue("UninstallString") as string;
                if (!string.IsNullOrWhiteSpace(uninstall))
                {
                    var m = Regex.Match(uninstall, @"^\s*""?(.+?\.exe)");
                    if (m.Success)
                    {
                        var parent = Path.GetDirectoryName(m.Groups[1].Value);
                        if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent)) return parent;
                    }
                }
            }
        }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Voicemeeter");
    }

    private static (string? Render8, string? Render16, string? Capture, string? Render8Name, string? Render16Name, string? CaptureName) ReadEndpoints()
    {
        string? r8 = null, r16 = null, cap = null, r8n = null, r16n = null, capn = null;
        using var renderRoot = Registry.LocalMachine.OpenSubKey(PmDevRender);
        if (renderRoot is not null)
        {
            foreach (var name in renderRoot.GetSubKeyNames())
            {
                using var device = renderRoot.OpenSubKey(name);
                if (device is null) continue;
                int? state = SafeInt(device.GetValue("DeviceState"));
                if (state != DeviceStateActive) continue;
                using var props = device.OpenSubKey("Properties");
                if (props is null) continue;
                var desc = props.GetValue(PkeyDesc) as string;
                if (!string.Equals(desc, "VB-Audio Virtual Cable", StringComparison.Ordinal)) continue;
                var form = SafeInt(props.GetValue(PkeyFormFactor)) ?? 0;
                var friendly = props.GetValue(PkeyFriendly) as string ?? string.Empty;
                if (form == 2 && r16 is null) { r16 = name; r16n = friendly; }
                else if (r8 is null) { r8 = name; r8n = friendly; }
            }
        }
        using var captureRoot = Registry.LocalMachine.OpenSubKey(PmDevCapture);
        if (captureRoot is not null)
        {
            foreach (var name in captureRoot.GetSubKeyNames())
            {
                using var device = captureRoot.OpenSubKey(name);
                if (device is null) continue;
                int? state = SafeInt(device.GetValue("DeviceState"));
                if (state != DeviceStateActive) continue;
                using var props = device.OpenSubKey("Properties");
                if (props is null) continue;
                var desc = props.GetValue(PkeyDesc) as string;
                if (!string.Equals(desc, "VB-Audio Virtual Cable", StringComparison.Ordinal)) continue;
                cap = name;
                capn = props.GetValue(PkeyFriendly) as string ?? string.Empty;
                break;
            }
        }
        return (r8, r16, cap, r8n, r16n, capn);
    }

    private static int? SafeInt(object? value)
    {
        return value switch
        {
            int i => i,
            long l => (int)l,
            string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) => p,
            _ => null
        };
    }

    /// <summary>
    /// One-click install of the whole stack: VB-CABLE + Voicemeeter, ReaPlugs,
    /// Equalizer APO, HeSuVi, LEQ Control Panel, library + HRIR + JSFX + VST
    /// placement, and endpoint rename/icons. Elevates.
    /// </summary>
    public async Task<bool> InstallStackAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var switches = new List<string> { "-InstallStack", "-InstallLibrary", "-SetupEndpoints", "-RenameVoicemeeter" };
        return await RunScriptAsync(switches, progress, cancellationToken);
    }

    /// <summary>
    /// One-click apply of a tune from the installed library, mirroring the
    /// ArtTuneDB guided walkthrough: writes config.txt paths for the chosen game
    /// + version (8ch and/or 16ch chains), and sets the LEQ release time
    /// (2 = Insta, 3 = Quick) when requested. Elevates.
    /// </summary>
    public async Task<bool> ApplyTuneAsync(
        string game,
        string version,
        string? sixteenChFile = null,
        string? eqFile = null,
        int leqReleaseTime = 0,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var switches = new List<string>
        {
            "-Game", RequireSafeArg(game, nameof(game)),
            "-Version", RequireSafeArg(version, nameof(version))
        };
        if (!string.IsNullOrWhiteSpace(sixteenChFile))
            switches.AddRange(new[] { "-SixteenChFile", RequireSafeArg(sixteenChFile, nameof(sixteenChFile)) });
        if (!string.IsNullOrWhiteSpace(eqFile))
            switches.AddRange(new[] { "-EqFile", RequireSafeArg(eqFile, nameof(eqFile)) });
        if (leqReleaseTime is >= 2 and <= 7)
            switches.AddRange(new[] { "-LeqReleaseTime", leqReleaseTime.ToString(CultureInfo.InvariantCulture) });
        return await RunScriptAsync(switches, progress, cancellationToken);
    }

    /// <summary>
    /// Full rollback: uninstalls ReaPlugs, Equalizer APO, VB-CABLE and
    /// Voicemeeter, removes the ArtTuneDB library / HeSuVi / JSFX / VST / LEQ,
    /// clears the endpoint renames, icons and LEQ release-time values, restores
    /// config.txt, and prunes leftover VB-Audio driver packages. Elevates.
    /// </summary>
    public async Task<bool> UninstallEverythingAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await RunScriptAsync(new[] { "-UninstallEverything" }, progress, cancellationToken);
    }

    /// <summary>
    /// Uninstalls only the apps (ReaPlugs, Equalizer APO, VB-CABLE, Voicemeeter).
    /// Elevates.
    /// </summary>
    public async Task<bool> UninstallStackAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await RunScriptAsync(new[] { "-UninstallStack" }, progress, cancellationToken);
    }

    /// <summary>
    /// Removes only the ArtTuneDB library, HeSuVi, JSFX and VST plugins.
    /// Elevates.
    /// </summary>
    public async Task<bool> UninstallLibraryAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await RunScriptAsync(new[] { "-UninstallLibrary" }, progress, cancellationToken);
    }

    /// <summary>
    /// Only resets endpoint names/icons and LEQ release time back to stock.
    /// Elevates.
    /// </summary>
    public async Task<bool> ResetEndpointsAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await RunScriptAsync(new[] { "-ResetEndpoints" }, progress, cancellationToken);
    }

    private async Task<bool> RunScriptAsync(
        IReadOnlyList<string> switches,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(ScriptPath))
            throw new FileNotFoundException("ArtTune install script is missing.", ScriptPath);

        var logPath = Path.Combine(
            Path.GetTempPath(),
            $"arttune-{Guid.NewGuid():N}.log");
        File.WriteAllText(logPath, string.Empty);

        var sb = new StringBuilder();
        sb.Append("& '").Append(ScriptPath.Replace("'", "''")).Append('\'');
        foreach (var s in switches)
            sb.Append(' ').Append(s);
        sb.Append(" *>&1 | Tee-Object -FilePath '")
          .Append(logPath.Replace("'", "''"))
          .Append("' -Encoding ASCII; exit $LASTEXITCODE");

        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(sb.ToString()));
        var psPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell", "v1.0", "powershell.exe");

        var psi = new ProcessStartInfo
        {
            FileName = psPath,
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encoded}"
        };

        Process? process = null;
        try
        {
            process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start elevated PowerShell.");
        }
        catch (Exception ex)
        {
            progress?.Report($"[ARTTUNE] ERROR could not start elevated installer: {ex.Message}");
            return false;
        }

        var lastLength = 0L;
        while (!process.HasExited)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lastLength = StreamLog(logPath, lastLength, progress);
            await Task.Delay(250, cancellationToken);
        }
        StreamLog(logPath, lastLength, progress);

        var success = process.ExitCode == 0;
        progress?.Report(success ? "[ARTTUNE] RESULT:OK" : "[ARTTUNE] RESULT:FAILED");
        try { File.Delete(logPath); } catch { /* best effort */ }
        return success;
    }

    private static long StreamLog(string logPath, long offset, IProgress<string>? progress)
    {
        if (!File.Exists(logPath)) return offset;
        try
        {
            using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (stream.Length <= offset) return offset;
            stream.Seek(offset, SeekOrigin.Begin);
            using var reader = new StreamReader(stream, Encoding.ASCII);
            var line = reader.ReadLine();
            while (line is not null)
            {
                progress?.Report(line);
                line = reader.ReadLine();
            }
            return stream.Position;
        }
        catch { return offset; }
    }

    private static string RequireSafeArg(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value) || !SafeArgRegex.IsMatch(value))
            throw new ArgumentException($"'{paramName}' contains unsupported characters.");
        return value;
    }

    public static void OpenGuidedGuide()
    {
        try { Process.Start(new ProcessStartInfo(GuidedUrl) { UseShellExecute = true }); }
        catch { /* best effort */ }
    }

    /// <summary>A tune version discovered in the installed ArtTuneDB library.</summary>
    public sealed record InstalledTuneVersion(
        string Game,
        string Version,
        bool HasEightChannel,
        bool HasSixteenChannel,
        string? PreFile,
        string? PostFile,
        string? TargetFile,
        string? EqFile,
        IReadOnlyList<string> SixteenChFiles,
        int LeqReleaseTimeHint);

    /// <summary>
    /// Enumerates <c>library\&lt;GAME&gt;\&lt;VERSION&gt;</c> from the installed
    /// ArtTuneDB library the way the guided walkthrough presents it.
    /// </summary>
    public static List<InstalledTuneVersion> EnumerateLibrary()
    {
        var result = new List<InstalledTuneVersion>();
        var libRoot = Path.Combine(EqualizerApoRoot, "config", "ArtTuneDB", "library");
        if (!Directory.Exists(libRoot)) return result;

        foreach (var gameDir in Directory.EnumerateDirectories(libRoot)
                     .OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            var game = Path.GetFileName(gameDir);
            foreach (var versionDir in Directory.EnumerateDirectories(gameDir)
                         .OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                var version = Path.GetFileName(versionDir);
                var eqFlat = Path.Combine(versionDir, "eq", "Flat_EQ.txt");
                var pre = FindFile(versionDir, $"{game}_{version}_pre.txt");
                var post = FindFile(versionDir, $"{game}_{version}_post.txt");
                var target = FindFile(versionDir, $"{game}_Target_{version}.txt");
                var sixteen = Directory.EnumerateFiles(versionDir, $"{game}_{version}_16ch_*.txt")
                    .Where(p => !p.EndsWith("_16ch_Target.txt", StringComparison.OrdinalIgnoreCase))
                    .Select(Path.GetFileName)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .Select(name => name!)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var leqHint = 0;
                if (File.Exists(Path.Combine(versionDir, "LEQ - Release Time 2 (Insta).txt"))) leqHint = 2;
                else if (File.Exists(Path.Combine(versionDir, "LEQ - Release Time 3 (Quick).txt"))) leqHint = 3;
                else if (File.Exists(Path.Combine(versionDir, "LEQ - OFF.txt"))) leqHint = 0;

                result.Add(new InstalledTuneVersion(
                    game, version,
                    pre != null && post != null && target != null,
                    sixteen.Count > 0,
                    pre, post, target,
                    File.Exists(eqFlat) ? "eq\\Flat_EQ.txt" : null,
                    sixteen,
                    leqHint));
            }
        }
        return result;
    }

    private static string? FindFile(string dir, string name)
    {
        var path = Path.Combine(dir, name);
        return File.Exists(path) ? name.Replace('\\', '/') : null;
    }
}

/// <summary>Health of the live tuning, verified from what is actually applied.</summary>
public enum ArtTuneTuningHealth
{
    /// <summary>Stack + config + routing all in place; tuning is live.</summary>
    Active,

    /// <summary>Stack + config present but something is off (broken include, wrong tune, or output not routed).</summary>
    Partial,

    /// <summary>Stack or config missing entirely - nothing is tuned.</summary>
    Inactive
}

/// <summary>One snapshot of a full live verification of the Art Tune chain.</summary>
public sealed class ArtTuneTuningVerification
{
    public static ArtTuneTuningVerification Empty { get; } = new();

    public ArtTuneTuningHealth Health { get; init; } = ArtTuneTuningHealth.Inactive;
    public bool StackInstalled { get; init; }
    public bool ConfigPresent { get; init; }
    public bool ConfigHasMarker { get; init; }
    public int IncludeCount { get; init; }
    public IReadOnlyList<string> MissingFiles { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> DeviceSections { get; init; } = Array.Empty<string>();
    public bool ContainsExpectedTune { get; init; }
    public bool DefaultRenderRoutedToStack { get; init; }
    public string DefaultRenderName { get; init; } = string.Empty;
    public string DefaultCaptureName { get; init; } = string.Empty;
    public string? DetectedTune { get; init; }
    public string Summary { get; init; } = string.Empty;
}

/// <summary>Parsed view of EqualizerAPO's live config.txt (device + include lines).</summary>
public sealed class ArtTuneConfigSnapshot
{
    public bool Present { get; init; }
    public bool HasMarker { get; init; }
    public int IncludeCount { get; init; }
    public IReadOnlyList<string> MissingFiles { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> DeviceSections { get; init; } = Array.Empty<string>();
    public IReadOnlyList<(string Game, string Version)> DetectedTunes { get; init; } = Array.Empty<(string, string)>();
}

/// <summary>
/// Live verification of the Art Tune chain. Unlike <see cref="ArtTuneStackService.Detect"/>,
/// which only reports presence, this proves the tuning is actually working: the config.txt
/// that Equalizer APO loads, every include it points at, the tune it selects, and whether the
/// current default output is actually routed through a tuned endpoint.
/// </summary>
public static class ArtTuneVerifier
{
    private const string ConfigMarker = "# ArtTuneDB config.txt";

    private static readonly Regex IncludeRegex = new(
        @"^include\s*[:=]\s*(.*?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex DeviceRegex = new(
        @"^device\s*:\s*(.+?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex TunePairRegex = new(
        @"library[\\/]+([^\\/]+)[\\/]+([^\\/]+)(?:[\\/]|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Path of the live Equalizer APO config file.</summary>
    public static string ConfigTxtPath =>
        Path.Combine(ArtTuneStackService.EqualizerApoRoot, "config", "config.txt");

    /// <summary>
    /// Parses config.txt into includes (resolved against the config folder) plus any
    /// per-device sections. Testable - takes an explicit path. Returns an empty snapshot
    /// when the file is absent.
    /// </summary>
    public static ArtTuneConfigSnapshot ReadConfigTxt(string? configPath = null)
    {
        var path = configPath ?? ConfigTxtPath;
        if (!File.Exists(path))
            return new ArtTuneConfigSnapshot { Present = false };

        string text;
        try { text = File.ReadAllText(path); }
        catch { return new ArtTuneConfigSnapshot { Present = false }; }

        var configDir = Path.GetDirectoryName(Path.GetFullPath(path)) ?? string.Empty;

        var markers = text.Contains(ConfigMarker, StringComparison.OrdinalIgnoreCase);
        var includes = new List<string>();
        foreach (Match m in IncludeRegex.Matches(text))
        {
            var val = m.Groups[1].Value.Trim().Trim('"');
            if (val.Length == 0) continue;
            includes.Add(val);
        }

        var deviceSections = new List<string>();
        foreach (Match m in DeviceRegex.Matches(text))
            deviceSections.Add(m.Groups[1].Value.Trim());

        var missing = new List<string>();
        foreach (var inc in includes)
        {
            var full = Path.IsPathRooted(inc)
                ? inc
                : Path.Combine(configDir, inc.Replace('/', '\\'));
            if (!File.Exists(full)) missing.Add(inc);
        }

        var pairs = new List<(string, string)>();
        foreach (var inc in includes)
        {
            var mm = TunePairRegex.Match(inc.Replace('\\', '/'));
            if (!mm.Success) continue;
            var pair = (mm.Groups[1].Value, mm.Groups[2].Value);
            if (!pairs.Any(p => p.Item1 == pair.Item1 && p.Item2 == pair.Item2))
                pairs.Add(pair);
        }

        return new ArtTuneConfigSnapshot
        {
            Present = true,
            HasMarker = markers,
            IncludeCount = includes.Count,
            MissingFiles = missing,
            DeviceSections = deviceSections,
            DetectedTunes = pairs
        };
    }

    /// <summary>
    /// Full live verification. <paramref name="expectedTune"/> is "GAME/VERSION" (or
    /// "GAME  VERSION"); when provided the verifier checks the loaded config actually
    /// selects that tune so "configured but not working" never shows as green.
    /// </summary>
    public static ArtTuneTuningVerification VerifyTuning(string? expectedTune = null)
    {
        try
        {
            var state = ArtTuneStackService.Detect();
            var cfg = ReadConfigTxt();

            var stackOk = state.EqualizerApoInstalled && state.VbCableDetected && state.LibraryInstalled;
            var configOk = cfg.Present && cfg.HasMarker;
            var includesOk = cfg.IncludeCount > 0 && cfg.MissingFiles.Count == 0;

            string? detected = null;
            if (cfg.DetectedTunes.Count > 0)
            {
                var last = cfg.DetectedTunes[cfg.DetectedTunes.Count - 1];
                detected = $"{last.Game}/{last.Version}";
            }

            var expectedNorm = string.IsNullOrWhiteSpace(expectedTune)
                ? null
                : expectedTune.Replace("  ", "/").Replace('\\', '/').Trim();
            if (expectedNorm is not null && expectedNorm.Length > 0 && !expectedNorm.Contains('/'))
                expectedNorm = $"{expectedNorm}";
            expectedNorm = expectedNorm?.TrimEnd('/');

            var containsExpected = expectedNorm is null
                || cfg.DetectedTunes.Any(p =>
                    string.Equals($"{p.Game}/{p.Version}", expectedNorm, StringComparison.OrdinalIgnoreCase));

            var renderName = DefaultEndpointName(DataFlow.Render);
            var captureName = DefaultEndpointName(DataFlow.Capture);
            var renderRouted = IsStackEndpoint(renderName);

            ArtTuneTuningHealth health;
            string summary;

            if (!stackOk)
            {
                health = ArtTuneTuningHealth.Inactive;
                summary = state.MissingComponents.Count == 0
                    ? "Not installed - run 1-CLICK INSTALL STACK."
                    : $"Missing: {string.Join(", ", state.MissingComponents)}. Run 1-CLICK INSTALL STACK.";
            }
            else if (!configOk)
            {
                health = ArtTuneTuningHealth.Inactive;
                summary = "Equalizer APO is not loading a tuned config.txt - run APPLY TUNE.";
            }
            else if (!includesOk)
            {
                health = ArtTuneTuningHealth.Partial;
                summary = $"config.txt references {cfg.MissingFiles.Count} missing file(s): " +
                          string.Join(", ", cfg.MissingFiles.Take(3)) +
                          ". Re-run APPLY TUNE.";
            }
            else if (!containsExpected)
            {
                health = ArtTuneTuningHealth.Partial;
                summary = detected is null
                    ? $"config.txt is loaded but does not select {expectedNorm} - run APPLY TUNE."
                    : $"config.txt selects {detected}, not {expectedNorm} - run APPLY TUNE.";
            }
            else if (!renderRouted)
            {
                health = ArtTuneTuningHealth.Partial;
                summary = $"Tuned config is live, but your default output is \"{renderName}\". " +
                          "Set output to Art Tune / Art Tune + / Voicemeeter / Cable for the tuning to be audible.";
            }
            else
            {
                health = ArtTuneTuningHealth.Active;
                summary = detected is null
                    ? $"LIVE - {cfg.IncludeCount} includes, default output {renderName}."
                    : $"LIVE - {detected}, {cfg.IncludeCount} includes, default output {renderName}.";
            }

            return new ArtTuneTuningVerification
            {
                Health = health,
                StackInstalled = stackOk,
                ConfigPresent = cfg.Present,
                ConfigHasMarker = cfg.HasMarker,
                IncludeCount = cfg.IncludeCount,
                MissingFiles = cfg.MissingFiles,
                DeviceSections = cfg.DeviceSections,
                ContainsExpectedTune = containsExpected,
                DefaultRenderRoutedToStack = renderRouted,
                DefaultRenderName = renderName,
                DefaultCaptureName = captureName,
                DetectedTune = detected,
                Summary = summary
            };
        }
        catch
        {
            return ArtTuneTuningVerification.Empty;
        }
    }

    private static bool IsStackEndpoint(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        if (name.Contains("Art Tune", StringComparison.OrdinalIgnoreCase)) return true;
        if (name.Contains("CABLE", StringComparison.OrdinalIgnoreCase) &&
            name.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase)) return true;
        return name.Contains("Voicemeeter", StringComparison.OrdinalIgnoreCase);
    }

    private static string DefaultEndpointName(DataFlow flow)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var dev = enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia);
            return dev?.FriendlyName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}