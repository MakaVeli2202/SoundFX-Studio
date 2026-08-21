using SoundFXStudio.Services.Hrtf;
using Xunit;

namespace SoundFXStudio.Tests;

public class SofaHrtfLoaderTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    private static string GetSofaFixturePath()
    {
        // TestData/ is copied to output directory via .csproj
        return Path.Combine(AppContext.BaseDirectory, "TestData", "SimpleFreeFieldHRIR_1.0.sofa");
    }

    private static SofaHrtfLoader CreateLoader() => new();

    // ── Happy path ────────────────────────────────────────────────────────

    [Fact]
    public void Load_RealSofaFile_ReturnsSuccess()
    {
        var loader = CreateLoader();
        var result = loader.Load(GetSofaFixturePath());
        Assert.True(result.Success, result.ErrorMessage);
    }

    [Fact]
    public void Load_RealSofaFile_ProfileHasCorrectSampleRate()
    {
        var loader = CreateLoader();
        var result = loader.Load(GetSofaFixturePath());

        Assert.True(result.Success);
        Assert.Equal(48000, result.SampleRate);
    }

    [Fact]
    public void Load_RealSofaFile_ProfileHasCorrectIrLength()
    {
        var loader = CreateLoader();
        var result = loader.Load(GetSofaFixturePath());

        Assert.True(result.Success);
        Assert.Equal(256, result.IrLength);
    }

    [Fact]
    public void Load_RealSofaFile_ProfileHasEntries()
    {
        var loader = CreateLoader();
        var result = loader.Load(GetSofaFixturePath());

        Assert.True(result.Success);
        Assert.True(result.DirectionsLoaded > 0);
        Assert.Equal(result.DirectionsLoaded, result.Profile!.Entries.Length);
    }

    [Fact]
    public void Load_RealSofaFile_ProfileHasId()
    {
        var loader = CreateLoader();
        var result = loader.Load(GetSofaFixturePath());

        Assert.True(result.Success);
        Assert.StartsWith("sofa-", result.Profile!.Id);
    }

    [Fact]
    public void Load_RealSofaFile_ProfileHasName()
    {
        var loader = CreateLoader();
        var result = loader.Load(GetSofaFixturePath());

        Assert.True(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Profile!.Name));
    }

    [Fact]
    public void Load_RealSofaFile_EntriesHaveValidLeftIr()
    {
        var loader = CreateLoader();
        var result = loader.Load(GetSofaFixturePath());

        Assert.True(result.Success);
        foreach (var entry in result.Profile!.Entries)
        {
            Assert.NotNull(entry.LeftEarResponse);
            Assert.Equal(result.IrLength, entry.LeftEarResponse.Length);
            Assert.All(entry.LeftEarResponse, v => Assert.True(float.IsFinite(v), $"LeftEarResponse contains non-finite value: {v}"));
        }
    }

    [Fact]
    public void Load_RealSofaFile_EntriesHaveValidRightIr()
    {
        var loader = CreateLoader();
        var result = loader.Load(GetSofaFixturePath());

        Assert.True(result.Success);
        foreach (var entry in result.Profile!.Entries)
        {
            Assert.NotNull(entry.RightEarResponse);
            Assert.Equal(result.IrLength, entry.RightEarResponse.Length);
            Assert.All(entry.RightEarResponse, v => Assert.True(float.IsFinite(v), $"RightEarResponse contains non-finite value: {v}"));
        }
    }

    [Fact]
    public void Load_RealSofaFile_EntriesHaveValidElevationRange()
    {
        var loader = CreateLoader();
        var result = loader.Load(GetSofaFixturePath());

        Assert.True(result.Success);
        foreach (var entry in result.Profile!.Entries)
        {
            Assert.InRange(entry.ElevationDeg, -90.0, 90.0);
        }
    }

    [Fact]
    public void Load_RealSofaFile_EntriesHaveValidAzimuthRange()
    {
        var loader = CreateLoader();
        var result = loader.Load(GetSofaFixturePath());

        Assert.True(result.Success);
        foreach (var entry in result.Profile!.Entries)
        {
            Assert.InRange(entry.AzimuthDeg, -180.0, 180.0);
        }
    }

    [Fact]
    public void Load_RealSofaFile_HasMultipleElevations()
    {
        var loader = CreateLoader();
        var result = loader.Load(GetSofaFixturePath());

        Assert.True(result.Success);
        var elevations = result.Profile!.Entries
            .Select(e => (int)e.ElevationDeg)
            .Distinct()
            .ToList();
        Assert.True(elevations.Count > 1, $"Expected multiple elevations, got {elevations.Count}");
    }

    [Fact]
    public void Load_RealSofaFile_HasMultipleAzimuths()
    {
        var loader = CreateLoader();
        var result = loader.Load(GetSofaFixturePath());

        Assert.True(result.Success);
        var azimuths = result.Profile!.Entries
            .Select(e => (int)e.AzimuthDeg)
            .Distinct()
            .ToList();
        Assert.True(azimuths.Count > 1, $"Expected multiple azimuths, got {azimuths.Count}");
    }

    [Fact]
    public void Load_RealSofaFile_ProfileHasDescription()
    {
        var loader = CreateLoader();
        var result = loader.Load(GetSofaFixturePath());

        Assert.True(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Profile!.Description));
    }

    // ── Error handling ────────────────────────────────────────────────────

    [Fact]
    public void Load_NullPath_ReturnsFailure()
    {
        var loader = CreateLoader();
        var result = loader.Load(null!);
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void Load_EmptyPath_ReturnsFailure()
    {
        var loader = CreateLoader();
        var result = loader.Load("");
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void Load_NonexistentFile_ReturnsFailure()
    {
        var loader = CreateLoader();
        var result = loader.Load(@"C:\nonexistent\fake.sofa");
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void Load_InvalidFile_ReturnsFailure()
    {
        var loader = CreateLoader();
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "This is not a SOFA file");
            var result = loader.Load(tempFile);
            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    // ── Clone safety ──────────────────────────────────────────────────────

    [Fact]
    public void Load_RealSofaFile_ProfileCloneDoesNotShareEntries()
    {
        var loader = CreateLoader();
        var result = loader.Load(GetSofaFixturePath());

        Assert.True(result.Success);
        var clone = result.Profile!.Clone();

        // Modifying clone should not affect original
        var originalFirstIr = result.Profile.Entries[0].LeftEarResponse[0];
        clone.Entries[0].LeftEarResponse[0] = -999f;
        Assert.Equal(originalFirstIr, result.Profile.Entries[0].LeftEarResponse[0]);
    }

    // ── SOFA fixture → profile preparation → DSP-ready ───────────────────

    [Fact]
    public void Load_RealSofa_ThenPrepareFor48000_ProducesValidDspProfile()
    {
        var loader = CreateLoader();
        var result = loader.Load(GetSofaFixturePath());
        Assert.True(result.Success, result.ErrorMessage);

        var profile = result.Profile!;
        var preparer = new SoundFXStudio.Services.Hrtf.HrtfProfilePreparer();
        var prepared = preparer.Prepare(profile, 48000);

        Assert.Equal(48000, prepared.SampleRate);
        Assert.True(prepared.Entries.Length > 0);
        Assert.True(prepared.IrLength > 0);

        // All HRIR data must be finite
        foreach (var entry in prepared.Entries)
        {
            Assert.All(entry.LeftEarResponse, v => Assert.True(float.IsFinite(v)));
            Assert.All(entry.RightEarResponse, v => Assert.True(float.IsFinite(v)));
        }

        // Original unchanged
        Assert.Equal(result.SampleRate, profile.SampleRate);
        Assert.Equal(result.IrLength, profile.IrLength);
    }

    [Fact]
    public void Load_RealSofa_SampleRateDetected()
    {
        var loader = CreateLoader();
        var result = loader.Load(GetSofaFixturePath());
        Assert.True(result.Success);

        // The fixture is 48000 Hz — verify we detect it correctly
        Assert.Equal(48000, result.SampleRate);
    }
}
