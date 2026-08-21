using SoundFXStudio.Models;
using SoundFXStudio.Services.Hrtf;
using Xunit;

namespace SoundFXStudio.Tests;

public class HrtfProfilePreparerTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    private static HrtfProfile CreateTestProfile(int sampleRate = 48000, int irLength = 128, int entryCount = 3)
    {
        var entries = new HrtfEntry[entryCount];
        for (int i = 0; i < entryCount; i++)
        {
            var left = new float[irLength];
            var right = new float[irLength];
            left[0] = 1.0f;
            right[0] = 0.9f;
            if (irLength > 4) left[4] = 0.3f;
            if (irLength > 4) right[4] = 0.25f;

            entries[i] = new HrtfEntry
            {
                AzimuthDeg = i * 45 - 90,
                ElevationDeg = i * 30 - 30,
                LeftEarResponse = left,
                RightEarResponse = right
            };
        }

        return new HrtfProfile
        {
            Id = "test-profile",
            Name = "Test Profile",
            Manufacturer = "Test",
            Description = "Test",
            SampleRate = sampleRate,
            IrLength = irLength,
            Entries = entries,
            DataSource = "Test",
            License = "Test"
        };
    }

    // ── Matching sample rate ──────────────────────────────────────────────

    [Fact]
    public void Prepare_SameRate_DoesNotResample()
    {
        var profile = CreateTestProfile(48000);
        var preparer = new HrtfProfilePreparer();

        var prepared = preparer.Prepare(profile, 48000);

        Assert.Equal(48000, prepared.SampleRate);
        Assert.Equal(128, prepared.IrLength);
    }

    [Fact]
    public void Prepare_SameRate_ReturnsClone()
    {
        var profile = CreateTestProfile(48000);
        var preparer = new HrtfProfilePreparer();

        var prepared = preparer.Prepare(profile, 48000);

        // Modifying original should not affect prepared
        profile.Entries[0].LeftEarResponse[0] = -999f;
        Assert.Equal(1.0f, prepared.Entries[0].LeftEarResponse[0]);
    }

    // ── Mismatching sample rate ───────────────────────────────────────────

    [Fact]
    public void Prepare_44100To48000_Resamples()
    {
        var profile = CreateTestProfile(44100, 256);
        var preparer = new HrtfProfilePreparer();

        var prepared = preparer.Prepare(profile, 48000);

        Assert.Equal(48000, prepared.SampleRate);
        Assert.True(prepared.IrLength > 256,
            $"Expected IrLength > 256, got {prepared.IrLength}");
    }

    [Fact]
    public void Prepare_48000To44100_Resamples()
    {
        var profile = CreateTestProfile(48000, 256);
        var preparer = new HrtfProfilePreparer();

        var prepared = preparer.Prepare(profile, 44100);

        Assert.Equal(44100, prepared.SampleRate);
        Assert.True(prepared.IrLength < 256,
            $"Expected IrLength < 256, got {prepared.IrLength}");
    }

    // ── Original profile unchanged ────────────────────────────────────────

    [Fact]
    public void Prepare_OriginalProfileUnchanged()
    {
        var profile = CreateTestProfile(44100, 256);
        var preparer = new HrtfProfilePreparer();

        _ = preparer.Prepare(profile, 48000);

        Assert.Equal(44100, profile.SampleRate);
        Assert.Equal(256, profile.IrLength);
        Assert.Equal(1.0f, profile.Entries[0].LeftEarResponse[0]);
    }

    // ── HRIR data is resampled ────────────────────────────────────────────

    [Fact]
    public void Prepare_LeftEarResponseResampled()
    {
        var profile = CreateTestProfile(44100, 256);
        var preparer = new HrtfProfilePreparer();

        var prepared = preparer.Prepare(profile, 48000);

        // Left ear should have more samples
        Assert.True(prepared.Entries[0].LeftEarResponse.Length > 256);
    }

    [Fact]
    public void Prepare_RightEarResponseResampled()
    {
        var profile = CreateTestProfile(44100, 256);
        var preparer = new HrtfProfilePreparer();

        var prepared = preparer.Prepare(profile, 48000);

        Assert.True(prepared.Entries[0].RightEarResponse.Length > 256);
    }

    [Fact]
    public void Prepare_AllEntriesConverted()
    {
        var profile = CreateTestProfile(44100, 256, entryCount: 5);
        var preparer = new HrtfProfilePreparer();

        var prepared = preparer.Prepare(profile, 48000);

        Assert.Equal(5, prepared.Entries.Length);
        foreach (var entry in prepared.Entries)
        {
            Assert.Equal(48000, prepared.SampleRate);
            Assert.True(entry.LeftEarResponse.Length > 256);
            Assert.True(entry.RightEarResponse.Length > 256);
        }
    }

    // ── Metadata preserved ────────────────────────────────────────────────

    [Fact]
    public void Prepare_AzimuthPreserved()
    {
        var profile = CreateTestProfile(44100);
        var preparer = new HrtfProfilePreparer();

        var prepared = preparer.Prepare(profile, 48000);

        for (int i = 0; i < profile.Entries.Length; i++)
        {
            Assert.Equal(profile.Entries[i].AzimuthDeg, prepared.Entries[i].AzimuthDeg);
        }
    }

    [Fact]
    public void Prepare_ElevationPreserved()
    {
        var profile = CreateTestProfile(44100);
        var preparer = new HrtfProfilePreparer();

        var prepared = preparer.Prepare(profile, 48000);

        for (int i = 0; i < profile.Entries.Length; i++)
        {
            Assert.Equal(profile.Entries[i].ElevationDeg, prepared.Entries[i].ElevationDeg);
        }
    }

    [Fact]
    public void Prepare_MetadataPreserved()
    {
        var profile = CreateTestProfile(44100);
        var preparer = new HrtfProfilePreparer();

        var prepared = preparer.Prepare(profile, 48000);

        Assert.Equal(profile.Id, prepared.Id);
        Assert.Equal(profile.Name, prepared.Name);
        Assert.Equal(profile.Manufacturer, prepared.Manufacturer);
        Assert.Equal(profile.Description, prepared.Description);
        Assert.Equal(profile.DataSource, prepared.DataSource);
        Assert.Equal(profile.License, prepared.License);
    }

    [Fact]
    public void Prepare_SampleRateUpdated()
    {
        var profile = CreateTestProfile(44100);
        var preparer = new HrtfProfilePreparer();

        var prepared = preparer.Prepare(profile, 48000);

        Assert.Equal(48000, prepared.SampleRate);
    }

    [Fact]
    public void Prepare_IrLengthUpdated()
    {
        var profile = CreateTestProfile(44100, 256);
        var preparer = new HrtfProfilePreparer();

        var prepared = preparer.Prepare(profile, 48000);

        var expectedLength = (int)Math.Round(256.0 * 48000 / 44100);
        Assert.Equal(expectedLength, prepared.IrLength);
    }

    // ── Independent entries ───────────────────────────────────────────────

    [Fact]
    public void Prepare_MultipleEntries_IndependentData()
    {
        var entries = new HrtfEntry[3];
        for (int i = 0; i < 3; i++)
        {
            var left = new float[256];
            var right = new float[256];
            left[0] = 1.0f;
            right[0] = 0.9f;
            left[i * 4] = 0.5f; // unique per entry

            entries[i] = new HrtfEntry
            {
                AzimuthDeg = i * 45 - 90,
                ElevationDeg = 0,
                LeftEarResponse = left,
                RightEarResponse = right
            };
        }

        var profile = new HrtfProfile
        {
            Id = "test", Name = "Test", SampleRate = 44100, IrLength = 256,
            Entries = entries, Manufacturer = "T", Description = "T",
            DataSource = "T", License = "T"
        };
        var preparer = new HrtfProfilePreparer();

        var prepared = preparer.Prepare(profile, 48000);

        // Entry 0 has peak at index 0, entry 1 at index 4, entry 2 at index 8
        Assert.NotEqual(
            prepared.Entries[0].LeftEarResponse[4],
            prepared.Entries[1].LeftEarResponse[4]);
    }

    // ── Cache behavior ────────────────────────────────────────────────────

    [Fact]
    public void Prepare_CachedResult_SameInstance()
    {
        var profile = CreateTestProfile(44100);
        var preparer = new HrtfProfilePreparer();

        var p1 = preparer.Prepare(profile, 48000);
        var p2 = preparer.Prepare(profile, 48000);

        Assert.Same(p1, p2);
    }

    [Fact]
    public void Prepare_DifferentTargetRate_DifferentResult()
    {
        var profile = CreateTestProfile(44100, 256);
        var preparer = new HrtfProfilePreparer();

        var p48 = preparer.Prepare(profile, 48000);
        var p96 = preparer.Prepare(profile, 96000);

        Assert.NotSame(p48, p96);
        Assert.NotEqual(p48.IrLength, p96.IrLength);
    }

    [Fact]
    public void ClearCache_ForcesResample()
    {
        var profile = CreateTestProfile(44100);
        var preparer = new HrtfProfilePreparer();

        var p1 = preparer.Prepare(profile, 48000);
        preparer.ClearCache();
        var p2 = preparer.Prepare(profile, 48000);

        Assert.NotSame(p1, p2);
    }

    [Fact]
    public void Invalidate_RemovesSpecificProfile()
    {
        var p1 = CreateTestProfile(44100);
        p1 = new HrtfProfile
        {
            Id = "p1", Name = "P1", SampleRate = 44100, IrLength = 128,
            Entries = p1.Entries, Manufacturer = "T", Description = "T",
            DataSource = "T", License = "T"
        };
        var p2 = CreateTestProfile(44100);
        p2 = new HrtfProfile
        {
            Id = "p2", Name = "P2", SampleRate = 44100, IrLength = 128,
            Entries = p2.Entries, Manufacturer = "T", Description = "T",
            DataSource = "T", License = "T"
        };

        var preparer = new HrtfProfilePreparer();
        var r1a = preparer.Prepare(p1, 48000);
        var r2a = preparer.Prepare(p2, 48000);

        preparer.Invalidate("p1");

        var r1b = preparer.Prepare(p1, 48000);
        var r2b = preparer.Prepare(p2, 48000);

        Assert.NotSame(r1a, r1b); // p1 was invalidated
        Assert.Same(r2a, r2b);    // p2 was not
    }
}
