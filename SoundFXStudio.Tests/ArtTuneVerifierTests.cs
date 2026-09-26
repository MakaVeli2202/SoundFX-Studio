using System.IO;
using Xunit;
using SoundFXStudio.Services.ArtTune;

namespace SoundFXStudio.Tests;

public sealed class ArtTuneVerifierTests : IDisposable
{
    private readonly string _root;

    public ArtTuneVerifierTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"arttune-verifier-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private void WriteFiles(params (string Rel, string Content)[] files)
    {
        foreach (var (rel, content) in files)
        {
            var full = Path.Combine(_root, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }
    }

    private string ConfigPath(string name = "config.txt") => Path.Combine(_root, name);

    [Fact]
    public void ReadConfigTxt_WhenFileMissing_ReportsNotPresent()
    {
        var snap = ArtTuneVerifier.ReadConfigTxt(Path.Combine(_root, "nope.txt"));
        Assert.False(snap.Present);
        Assert.Equal(0, snap.IncludeCount);
    }

    [Fact]
    public void ReadConfigTxt_ResolvesIncludesAndDetectsTune()
    {
        WriteFiles(
            ("config.txt", ArtTuneConfig(
                "Art Tune VB-Audio Virtual Cable {aaa}",
                new[]
                {
                    (@"ArtTuneDB\library\BO6\5\BO6_5_pre.txt", ""),
                    (@"ArtTuneDB\library\BO6\5\eq\Flat_EQ.txt", ""),
                    (@"ArtTuneDB\boost.txt", "")
                })),
            (@"ArtTuneDB\library\BO6\5\BO6_5_pre.txt", ""),
            (@"ArtTuneDB\library\BO6\5\eq\Flat_EQ.txt", ""),
            (@"ArtTuneDB\boost.txt", ""));

        var snap = ArtTuneVerifier.ReadConfigTxt(ConfigPath());

        Assert.True(snap.Present);
        Assert.True(snap.HasMarker);
        Assert.Equal(3, snap.IncludeCount);
        Assert.Empty(snap.MissingFiles);
        Assert.Single(snap.DeviceSections);
        Assert.Equal("Art Tune VB-Audio Virtual Cable {aaa}", snap.DeviceSections[0]);
        var (game, version) = snap.DetectedTunes.Single();
        Assert.Equal("BO6", game);
        Assert.Equal("5", version);
    }

    [Fact]
    public void ReadConfigTxt_FlagsMissingIncludeFiles()
    {
        WriteFiles(
            ("config.txt", ArtTuneConfig(null, new[] { (@"ArtTuneDB\library\BO6\5\BO6_5_pre.txt", "") })));

        var snap = ArtTuneVerifier.ReadConfigTxt(ConfigPath());

        Assert.Equal(1, snap.IncludeCount);
        var missing = Assert.Single(snap.MissingFiles);
        Assert.StartsWith("ArtTuneDB", missing);
    }

    [Fact]
    public void ReadConfigTxt_TreatsPlaceholderLibraryAsMissing()
    {
        // The PS1 writes "Include: ArtTuneDB\library\" as a placeholder when the
        // tune file was not found - this must NOT look healthy.
        WriteFiles(
            ("config.txt", ArtTuneConfig(null, new[] { (@"ArtTuneDB\library\", "") })));

        var snap = ArtTuneVerifier.ReadConfigTxt(ConfigPath());

        Assert.Equal(1, snap.IncludeCount);
        var missing = Assert.Single(snap.MissingFiles);
        Assert.Equal(@"ArtTuneDB\library\", missing);
        Assert.Empty(snap.DetectedTunes);
    }

    [Fact]
    public void ReadConfigTxt_NoMarkerWhenUserConfigPresent()
    {
        WriteFiles(("config.txt", "Preamp: -6 dB\nGraphicEQ: f 50;g 3"));

        var snap = ArtTuneVerifier.ReadConfigTxt(ConfigPath());

        Assert.True(snap.Present);
        Assert.False(snap.HasMarker);
        Assert.Equal(0, snap.IncludeCount);
    }

    private static string ArtTuneConfig(string? deviceLine, (string Include, string Content)[] includes)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# ArtTuneDB config.txt");
        sb.AppendLine("# Complete each \"ArtTuneDB\\library\\\" path below");
        sb.AppendLine();
        if (deviceLine is not null)
        {
            sb.AppendLine($"Device: {deviceLine}");
            sb.AppendLine();
        }
        foreach (var (inc, _) in includes)
        {
            sb.AppendLine($"Include: {inc}");
        }
        return sb.ToString();
    }
}