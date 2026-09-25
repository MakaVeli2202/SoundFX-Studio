using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using SoundFXStudio.Models;
using SoundFXStudio.Services.Hrtf;

namespace SoundFXStudio.Services.ArtTune;

/// <summary>
/// Downloads, parses and caches tune data from the ArtTuneDB GitHub repository
/// (https://github.com/ArtIsWar/ArtTuneDB):
///   - Headphone EQ profiles   : UsyTrace measurements in library/measurements/
///   - Game tunes              : EqualizerAPO style presets clustered per game
///   - HRTF                    : 14-channel EAC default HRIR (hrir/EAC_Default.wav)
///
/// Raw repo files are cached under %LocalAppData%\SoundFXStudio\ArtTune\ so
/// profiles can be loaded offline. The library version.txt is used to make
/// startup checks cheap (no bulk download when the library is unchanged).
/// </summary>
public sealed class ArtTuneLibraryService : IDisposable
{
    public const string HrtfProfileId = "arttune-eac-default";

    private const string RepoRawBase = "https://raw.githubusercontent.com/ArtIsWar/ArtTuneDB/main/";
    private const string RepoTreeUrl = "https://api.github.com/repos/ArtIsWar/ArtTuneDB/git/trees/main?recursive=1";
    private const string VersionPath = "library/version.txt";
    private const string DatabasePath = "library/measurements/database.json";
    private const string MeasurementsRoot = "library/measurements/";
    private const string EqRoot = "library/eq/";
    private const string HrirPath = "hrir/EAC_Default.wav";

    private const string HeadphoneIdPrefix = "arttune-head-";
    private const string GamingIdPrefix = "arttune-gaming-";

    private static readonly HttpClient Client = CreateClient();
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly Regex FilterRegex = new(
        @"^Filter\s+\d+\s*[:.]\s*(ON|OFF)\s+([A-Za-z]+)\s+Fc\s+([\d.]+)\s+Hz\s+Gain\s+([-+\d.]+)\s+dB\s+Q\s+([\d.]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PreampRegex = new(
        @"^Preamp\s*:\s*([-+\d.]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly double[] IsoThirdOctaveCenters =
    {
        20, 25, 31.5, 40, 50, 63, 80, 100, 125, 160, 200, 250, 315, 400, 500, 630, 800, 1000,
        1250, 1600, 2000, 2500, 3150, 4000, 5000, 6300, 8000, 10000, 12500, 16000, 20000
    };

    private readonly IHrtfProfileStore _profileStore;
    private readonly string _cacheRoot;
    private readonly string _measurementsCacheDir;
    private readonly string _tunesCacheDir;
    private readonly string _hrirCacheDir;
    private bool _disposed;

    public ArtTuneLibraryService(IHrtfProfileStore? profileStore = null)
    {
        _profileStore = profileStore ?? new HrtfProfileStore();
        _cacheRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SoundFXStudio", "ArtTune");
        _measurementsCacheDir = Path.Combine(_cacheRoot, "measurements");
        _tunesCacheDir = Path.Combine(_cacheRoot, "tunes");
        _hrirCacheDir = Path.Combine(_cacheRoot, "hrir");
    }

    public List<HeadphoneProfile> HeadphoneProfiles { get; } = new();
    public List<GamingProfile> GamingProfiles { get; } = new();
    public HrtfProfile? HrtfProfile { get; private set; }

    public string CachedVersion
    {
        get
        {
            try
            {
                var path = Path.Combine(_cacheRoot, "version.txt");
                return File.Exists(path) ? File.ReadAllText(path).Trim() : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    /// <summary>
    /// Rebuilds in-memory profiles from the local cache without any network I/O.
    /// Returns the cached library version (empty when nothing is cached).
    /// </summary>
    public string LoadFromCache()
    {
        HeadphoneProfiles.Clear();
        GamingProfiles.Clear();
        HrtfProfile = null;

        string version = CachedVersion;

        if (Directory.Exists(_measurementsCacheDir))
        {
            foreach (var file in Directory.EnumerateFiles(_measurementsCacheDir, "*.txt", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var profile = ParseMeasurementProfile(file);
                    if (profile is not null)
                        HeadphoneProfiles.Add(profile);
                }
                catch { /* skip corrupt cache entry */ }
            }
        }

        if (Directory.Exists(_tunesCacheDir))
        {
            foreach (var file in Directory.EnumerateFiles(_tunesCacheDir, "*.txt", SearchOption.AllDirectories))
            {
                try
                {
                    var profile = ParseTuneProfile(file);
                    if (profile is not null)
                        GamingProfiles.Add(profile);
                }
                catch { /* skip corrupt cache entry */ }
            }
        }

        var hrirFile = Path.Combine(_hrirCacheDir, HrirPath);
        if (File.Exists(hrirFile))
        {
            try
            {
                HrtfProfile = HeSuviHrirDecoder.DecodeEacDefault(File.ReadAllBytes(hrirFile));
            }
            catch { /* skip corrupt cache entry */ }
        }

        return version;
    }

    /// <summary>
    /// Runs a full library sync. Returns early (IsCurrent) when the cached library
    /// already matches the remote version.
    /// </summary>
    public async Task<ArtTuneUpdateResult> UpdateAsync(Action<string>? progress = null)
    {
        try
        {
            progress?.Invoke("Fetching ArtTuneDB metadata…");

            var version = (await GetStringAsync(VersionPath)).Trim();
            if (string.IsNullOrWhiteSpace(version))
                throw new InvalidOperationException("ArtTuneDB version.txt was empty.");

            if (!string.IsNullOrEmpty(CachedVersion) && CachedVersion.Equals(version, StringComparison.OrdinalIgnoreCase))
            {
                LoadFromCache();
                return new ArtTuneUpdateResult
                {
                    Updated = false,
                    IsCurrent = true,
                    Version = version,
                    Message = $"Tune library is already current (v {version})"
                };
            }

            progress?.Invoke($"Downloading tune library v {version}…");
            var tree = await GetTreeAsync();

            var measurePaths = tree
                .Where(IsMeasurementFile)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var tunePaths = tree
                .Where(IsTuneFile)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();

            HeadphoneProfiles.Clear();
            GamingProfiles.Clear();
            HrtfProfile = null;

            Dictionary<string, (string Brand, string Model)>? metadata = null;
            try
            {
                var dbJson = await GetStringAsync(DatabasePath);
                metadata = ParseDatabase(dbJson);
                progress?.Invoke($"Library inventory: {measurePaths.Count} headphone measurements, {tunePaths.Count} game tunes.");
            }
            catch
            {
                progress?.Invoke($"Library inventory: {measurePaths.Count} headphone measurements, {tunePaths.Count} game tunes. (metadata unavailable)");
            }

            Directory.CreateDirectory(_measurementsCacheDir);
            int headphoneCount = 0;
            foreach (var path in measurePaths)
            {
                var fileName = Path.GetFileName(path);
                var cachePath = Path.Combine(_measurementsCacheDir, fileName);
                var text = await DownloadAndCache(path, cachePath);
                if (text is null)
                    continue;

                string brand = "ArtTune";
                string model = CleanName(fileName);
                if (metadata is { } map && map.TryGetValue(fileName, out var meta))
                {
                    if (!string.IsNullOrWhiteSpace(meta.Brand)) brand = meta.Brand;
                    if (!string.IsNullOrWhiteSpace(meta.Model)) model = CleanName(meta.Model);
                }

                var profile = ParseMeasurementContent(text, brand, model);
                if (profile is not null)
                {
                    HeadphoneProfiles.Add(profile);
                    headphoneCount++;
                }
            }

            Directory.CreateDirectory(_tunesCacheDir);
            int tuneCount = 0;
            foreach (var path in tunePaths)
            {
                var rel = GetRelativeTuneName(path);
                var cachePath = Path.Combine(_tunesCacheDir, rel + ".txt");
                var text = await DownloadAndCache(path, cachePath);
                if (text is null)
                    continue;

                var profile = ParseTuneContent(path, text);
                if (profile is not null)
                {
                    GamingProfiles.Add(profile);
                    tuneCount++;
                }
            }

            int hrtfCount = 0;
            try
            {
                progress?.Invoke("Downloading EAC default HRIR…");
                Directory.CreateDirectory(_hrirCacheDir);
                var hrirCachePath = Path.Combine(_hrirCacheDir, "EAC_Default.wav");
                var hrirBytes = await Client.GetByteArrayAsync(RepoRawBase + HrirPath);
                File.WriteAllBytes(hrirCachePath, hrirBytes);

                var hrtf = HeSuviHrirDecoder.DecodeEacDefault(hrirBytes);
                if (hrtf is not null)
                {
                    HrtfProfile = hrtf;
                    _profileStore.Save(hrtf);
                    hrtfCount = 1;
                }
            }
            catch { /* HRIR is optional — keep going */ }

            WriteVersionCache(version);

            progress?.Invoke("Tune library update complete.");

            return new ArtTuneUpdateResult
            {
                Updated = true,
                Version = version,
                HeadphoneProfiles = headphoneCount,
                GamingProfiles = tuneCount,
                HrtfProfiles = hrtfCount,
                Message = $"Tune library updated (v {version}): {headphoneCount} headphone EQ, {tuneCount} game tunes, {hrtfCount} HRTF"
            };
        }
        catch (Exception ex)
        {
            return new ArtTuneUpdateResult
            {
                Updated = false,
                Error = ex.Message,
                Message = "Tune library update failed."
            };
        }
    }

    // ─── GitHub I/O ────────────────────────────────────────────────────────

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SoundFXStudio/1.0");
        client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private async Task<string> GetStringAsync(string repoPath)
    {
        using var response = await Client.GetAsync(RepoRawBase + repoPath);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<string?> DownloadAndCache(string repoPath, string cachePath)
    {
        try
        {
            var content = await GetStringAsync(repoPath);
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            File.WriteAllText(cachePath, content);
            return content;
        }
        catch
        {
            return null;
        }
    }

    private async Task<List<string>> GetTreeAsync()
    {
        using var response = await Client.GetAsync(RepoTreeUrl);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var tree = JsonSerializer.Deserialize<TreeResponse>(json, JsonOptions);
        if (tree?.Tree is null)
            throw new InvalidOperationException("ArtTuneDB tree response was empty.");
        if (tree.Truncated)
            throw new InvalidOperationException("ArtTuneDB tree response was truncated by GitHub.");

        return tree.Tree
            .Where(t => string.Equals(t.Type, "blob", StringComparison.OrdinalIgnoreCase))
            .Select(t => t.Path ?? string.Empty)
            .Where(p => p.Length > 0)
            .ToList();
    }

    private static Dictionary<string, (string Brand, string Model)>? ParseDatabase(string json)
    {
        var db = JsonSerializer.Deserialize<DatabaseResponse>(json, JsonOptions);
        if (db?.Entries is null)
            return null;

        var map = new Dictionary<string, (string Brand, string Model)>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in db.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.File))
                continue;
            map[entry.File] = (entry.Brand ?? string.Empty, entry.Model ?? string.Empty);
        }
        return map;
    }

    private static bool IsMeasurementFile(string path)
    {
        if (!path.StartsWith(MeasurementsRoot, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            return false;
        var rest = path.Substring(MeasurementsRoot.Length);
        if (rest.Length == 0 || rest.Contains('/') || rest.Contains('\\'))
            return false;
        return !path.EndsWith("database.json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTuneFile(string path)
    {
        if (!path.StartsWith("library/", StringComparison.OrdinalIgnoreCase))
            return false;
        if (path.StartsWith(MeasurementsRoot, StringComparison.OrdinalIgnoreCase))
            return false;
        if (path.StartsWith(EqRoot, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            return false;
        if (path.Contains("_Target_", StringComparison.OrdinalIgnoreCase))
            return false;

        var segments = path.Split('/', '\\');
        if (segments.Contains("eq", StringComparer.OrdinalIgnoreCase))
            return false;

        return true;
    }

    private static string GetRelativeTuneName(string path)
    {
        var trimmed = path;
        if (trimmed.StartsWith("library/", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed.Substring("library/".Length);
        var ext = Path.GetExtension(trimmed);
        return trimmed.EndsWith(ext, StringComparison.OrdinalIgnoreCase)
            ? trimmed.Substring(0, trimmed.Length - ext.Length)
            : trimmed;
    }

    // ─── Parsers ───────────────────────────────────────────────────────────

    private HeadphoneProfile? ParseMeasurementProfile(string filePath)
    {
        return ParseMeasurementContent(File.ReadAllText(filePath), brand: "ArtTune", model: CleanName(Path.GetFileName(filePath)));
    }

    internal static HeadphoneProfile? ParseMeasurementContent(string text, string brand, string model)
    {
        // UsyTrace export: comment lines begin with '*'; data rows are "freq<TAB>spl".
        var points = new List<(double Freq, double Spl)>();
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '*' || line[0] == ';' || line[0] == '#')
                continue;

            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                continue;

            if (double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var freq)
                && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var spl)
                && freq >= 15 && freq <= 25000)
            {
                points.Add((freq, spl));
            }
        }

        if (points.Count < 8)
            return null;

        points.Sort((a, b) => a.Freq.CompareTo(b.Freq));

        // Average SPL per 1/3-octave band (ISO centers 20 Hz … 20 kHz).
        var bandGains = new List<(double CenterHz, double GainDb)>();
        var bandLevels = new List<(double CenterHz, double Level)>();
        foreach (var center in IsoThirdOctaveCenters)
        {
            var lower = center * Math.Pow(2.0, -1.0 / 6.0);
            var upper = center * Math.Pow(2.0, 1.0 / 6.0);
            var inBand = points.Where(p => p.Freq >= lower && p.Freq <= upper).ToList();
            if (inBand.Count == 0)
                continue;
            bandLevels.Add((center, inBand.Average(p => p.Spl)));
        }

        if (bandLevels.Count == 0)
            return null;

        double reference = bandLevels.Average(b => b.Level);

        foreach (var (center, level) in bandLevels)
        {
            var gain = reference - level;
            if (Math.Abs(gain) < 0.3)
                continue;
            bandGains.Add((center, Math.Clamp(gain, -10.0, 10.0)));
        }

        if (bandGains.Count == 0)
            return null;

        double preamp = bandGains.Min(g => g.GainDb);
        preamp = Math.Min(0, preamp);

        var profile = new HeadphoneProfile
        {
            Id = HeadphoneIdPrefix + Slugify(model),
            Name = CleanName(model),
            Manufacturer = string.IsNullOrWhiteSpace(brand) ? "ArtTuneDB" : brand,
            Model = CleanName(model),
            Description = "Headphone EQ derived from a UsyTrace measurement in the ArtTuneDB library.",
            PreampDb = preamp
        };
        foreach (var (center, gain) in bandGains.OrderBy(g => g.CenterHz))
        {
            profile.Filters.Add(new EqFilter
            {
                Type = EqFilterType.Peaking,
                FrequencyHz = center,
                GainDb = gain,
                Q = 1.41,
                Enabled = true
            });
        }
        return profile;
    }

    private GamingProfile? ParseTuneProfile(string filePath)
    {
        return ParseTuneContent(filePath, File.ReadAllText(filePath));
    }

    internal static GamingProfile? ParseTuneContent(string repoPath, string text)
    {
        var filters = new List<EqFilter>();
        double preamp = 0;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;
            if (line[0] == '*' || line[0] == '#' || line[0] == ';')
                continue;
            if (line.StartsWith("Channel:", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("VSTPlugin:", StringComparison.OrdinalIgnoreCase))
                continue;

            var preampMatch = PreampRegex.Match(line);
            if (preampMatch.Success
                && double.TryParse(preampMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var pamp))
            {
                preamp = pamp;
                continue;
            }

            var match = FilterRegex.Match(line);
            if (!match.Success)
                continue;

            var type = MapFilterType(match.Groups[2].Value);
            if (!double.TryParse(match.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var freq)
                || !double.TryParse(match.Groups[4].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var gain)
                || !double.TryParse(match.Groups[5].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var q))
                continue;

            filters.Add(new EqFilter
            {
                Type = type,
                FrequencyHz = Math.Clamp(freq, 20, 20000),
                GainDb = Math.Clamp(gain, -30, 30),
                Q = Math.Clamp(q, 0.1, 30),
                Enabled = true
            });
        }

        if (filters.Count == 0)
        {
            var atk = AtkChunkDecoder.DecodeFirst(text);
            if (atk is null)
                return null;

            var atkSegments = repoPath.Replace('\\', '/').Split('/');
            var atkGame = atkSegments.Length > 1 ? atkSegments[1] : "Tune";
            var atkFileName = atkSegments.Length > 0 ? atkSegments[^1] : repoPath;
            var atkShortName = CleanName(Path.GetFileNameWithoutExtension(atkFileName));
            var atkSlug = Slugify(GetRelativeTuneName(repoPath));

            return new GamingProfile
            {
                Id = GamingIdPrefix + atkSlug,
                Name = atkShortName,
                Category = atkGame.ToUpperInvariant(),
                Game = atkGame,
                Description = $"Spatial-engine tune from the ArtTuneDB library ({atkGame}), decoded from the VST chunk. Requires the engine port — see AtkSpatialPreset.",
                PreampDb = preamp,
                EqEnabled = false,
                EqFilters = new System.Collections.ObjectModel.ObservableCollection<EqFilter>(filters),
                AtkPreset = atk,
                SpatialMode = atk.ChannelCount == 16 ? "16ch" : atk.Magic
            };
        }

        var segments = repoPath.Replace('\\', '/').Split('/');
        var game = segments.Length > 1 ? segments[1] : "Tune";
        var fileName = segments.Length > 0 ? segments[^1] : repoPath;
        var shortName = CleanName(Path.GetFileNameWithoutExtension(fileName));
        var slug = Slugify(GetRelativeTuneName(repoPath));

        return new GamingProfile
        {
            Id = GamingIdPrefix + slug,
            Name = shortName,
            Category = game.ToUpperInvariant(),
            Game = game,
            Description = $"Game tune from the ArtTuneDB library ({game}).",
            PreampDb = preamp,
            EqEnabled = true,
            EqFilters = new System.Collections.ObjectModel.ObservableCollection<EqFilter>(filters)
        };
    }

    private static EqFilterType MapFilterType(string token)
    {
        return token.ToUpperInvariant() switch
        {
            "PK" => EqFilterType.Peaking,
            "LSC" => EqFilterType.LowShelf,
            "HSC" => EqFilterType.HighShelf,
            "LP" => EqFilterType.LowPass,
            "HP" => EqFilterType.HighPass,
            "NCS" => EqFilterType.Notch,
            _ => EqFilterType.Peaking
        };
    }

    // ─── Helpers ───────────────────────────────────────────────────────────

    private void WriteVersionCache(string version)
    {
        try
        {
            Directory.CreateDirectory(_cacheRoot);
            File.WriteAllText(Path.Combine(_cacheRoot, "version.txt"), version);
        }
        catch { /* best effort */ }
    }

    private static string CleanName(string value)
    {
        var cleaned = value.Trim();
        if (cleaned.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned.Substring(0, cleaned.Length - 4);
        return cleaned.Replace('_', ' ');
    }

    private static string Slugify(string value)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var ch in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
            }
            else if (sb.Length > 0 && sb[^1] != '-')
            {
                sb.Append('-');
            }
        }
        while (sb.Length > 0 && sb[^1] == '-')
            sb.Length--;

        var slug = sb.ToString();
        return string.IsNullOrEmpty(slug) ? "profile" : slug;
    }

    private sealed class TreeResponse
    {
        public List<TreeItem>? Tree { get; set; }
        public bool Truncated { get; set; }
    }

    private sealed class TreeItem
    {
        public string? Path { get; set; }
        public string? Type { get; set; }
    }

    private sealed class DatabaseResponse
    {
        public List<DatabaseEntry>? Entries { get; set; }
    }

    private sealed class DatabaseEntry
    {
        public string? File { get; set; }
        public string? Brand { get; set; }
        public string? Model { get; set; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // Static HttpClient is intentionally shared and never disposed.
    }
}