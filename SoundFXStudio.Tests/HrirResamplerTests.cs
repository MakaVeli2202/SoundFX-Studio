using SoundFXStudio.Services.Hrtf;
using Xunit;

namespace SoundFXStudio.Tests;

public class HrirResamplerTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    private static float[] CreateImpulse(int length, int impulseIndex = 0, float gain = 1.0f)
    {
        var hrir = new float[length];
        if (impulseIndex >= 0 && impulseIndex < length)
            hrir[impulseIndex] = gain;
        return hrir;
    }

    private static float[] CreateDecayingImpulse(int length, int peakIndex = 0)
    {
        var hrir = new float[length];
        for (int i = 0; i < length; i++)
        {
            var dist = Math.Abs(i - peakIndex);
            hrir[i] = (float)Math.Exp(-0.1 * dist);
        }
        return hrir;
    }

    // ── Same rate (identity) ──────────────────────────────────────────────

    [Fact]
    public void Resample_SameRate_ReturnsSameLength()
    {
        var hrir = CreateImpulse(256);
        var result = HrirResampler.Resample(hrir, 48000, 48000);
        Assert.Equal(256, result.Length);
    }

    [Fact]
    public void Resample_SameRate_ReturnsSameData()
    {
        var hrir = CreateDecayingImpulse(128, 4);
        var result = HrirResampler.Resample(hrir, 48000, 48000);

        for (int i = 0; i < hrir.Length; i++)
        {
            Assert.Equal(hrir[i], result[i], 5);
        }
    }

    [Fact]
    public void Resample_SameRate_DoesNotShareArray()
    {
        var hrir = CreateImpulse(64);
        var result = HrirResampler.Resample(hrir, 48000, 48000);
        hrir[0] = -999f;
        Assert.NotEqual(-999f, result[0]);
    }

    // ── Upsample 44100 → 48000 ───────────────────────────────────────────

    [Fact]
    public void Resample_44100To48000_IncreasesLength()
    {
        var hrir = CreateImpulse(256);
        var result = HrirResampler.Resample(hrir, 44100, 48000);

        // Expected: 256 * 48000 / 44100 ≈ 279
        Assert.True(result.Length > 256,
            $"Expected length > 256, got {result.Length}");
    }

    [Fact]
    public void Resample_44100To48000_CorrectLength()
    {
        var hrir = CreateImpulse(256);
        var result = HrirResampler.Resample(hrir, 44100, 48000);

        var expected = (int)Math.Round(256.0 * 48000 / 44100);
        Assert.Equal(expected, result.Length);
    }

    // ── Downsample 48000 → 44100 ──────────────────────────────────────────

    [Fact]
    public void Resample_48000To44100_DecreasesLength()
    {
        var hrir = CreateImpulse(256);
        var result = HrirResampler.Resample(hrir, 48000, 44100);

        // Expected: 256 * 44100 / 48000 ≈ 235
        Assert.True(result.Length < 256,
            $"Expected length < 256, got {result.Length}");
    }

    [Fact]
    public void Resample_48000To44100_CorrectLength()
    {
        var hrir = CreateImpulse(256);
        var result = HrirResampler.Resample(hrir, 48000, 44100);

        var expected = (int)Math.Round(256.0 * 44100 / 48000);
        Assert.Equal(expected, result.Length);
    }

    // ── Extreme rate conversions ──────────────────────────────────────────

    [Fact]
    public void Resample_22050To48000_MoreThanDoublesLength()
    {
        var hrir = CreateImpulse(128);
        var result = HrirResampler.Resample(hrir, 22050, 48000);

        // Expected: 128 * 48000 / 22050 ≈ 279
        Assert.True(result.Length > 256,
            $"Expected length > 256, got {result.Length}");
    }

    [Fact]
    public void Resample_96000To48000_HalvesLength()
    {
        var hrir = CreateImpulse(512);
        var result = HrirResampler.Resample(hrir, 96000, 48000);

        // Expected: 512 * 48000 / 96000 = 256
        Assert.Equal(256, result.Length);
    }

    // ── Error handling ────────────────────────────────────────────────────

    [Fact]
    public void Resample_NullHrir_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            HrirResampler.Resample(null!, 48000, 48000));
    }

    [Fact]
    public void Resample_EmptyHrir_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            HrirResampler.Resample(Array.Empty<float>(), 48000, 48000));
    }

    [Fact]
    public void Resample_InvalidSourceRate_Throws()
    {
        var hrir = CreateImpulse(128);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HrirResampler.Resample(hrir, 1000, 48000));
    }

    [Fact]
    public void Resample_InvalidTargetRate_Throws()
    {
        var hrir = CreateImpulse(128);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HrirResampler.Resample(hrir, 48000, 200000));
    }

    // ── Quality checks ───────────────────────────────────────────────────

    [Fact]
    public void Resample_ImpulseRemainsImpulseLike()
    {
        // An impulse at index 0 should remain concentrated near index 0 after resampling
        var hrir = CreateImpulse(128, 0, 1.0f);
        var result = HrirResampler.Resample(hrir, 44100, 48000);

        // Peak should be in the first few samples
        var peakIndex = 0;
        var peakVal = result[0];
        for (int i = 1; i < Math.Min(5, result.Length); i++)
        {
            if (result[i] > peakVal)
            {
                peakVal = result[i];
                peakIndex = i;
            }
        }
        Assert.True(peakIndex < 5, $"Peak at index {peakIndex}, expected near 0");
        Assert.True(peakVal > 0.5f, $"Peak value {peakVal}, expected > 0.5");
    }

    [Fact]
    public void Resample_AllOutputFinite()
    {
        var hrir = CreateDecayingImpulse(256, 8);
        var result = HrirResampler.Resample(hrir, 44100, 48000);
        Assert.All(result, v => Assert.True(float.IsFinite(v), $"Non-finite value: {v}"));
    }

    [Fact]
    public void Resample_Deterministic()
    {
        var hrir = CreateDecayingImpulse(256, 4);
        var r1 = HrirResampler.Resample(hrir, 44100, 48000);
        var r2 = HrirResampler.Resample(hrir, 44100, 48000);

        Assert.Equal(r1.Length, r2.Length);
        for (int i = 0; i < r1.Length; i++)
            Assert.Equal(r1[i], r2[i]);
    }

    [Fact]
    public void Resample_PreservesEnergy()
    {
        // A decaying impulse should have roughly similar total energy after resampling
        var hrir = CreateDecayingImpulse(256, 16);
        var result = HrirResampler.Resample(hrir, 44100, 48000);

        double inputEnergy = 0, outputEnergy = 0;
        for (int i = 0; i < hrir.Length; i++) inputEnergy += hrir[i] * hrir[i];
        for (int i = 0; i < result.Length; i++) outputEnergy += result[i] * result[i];

        // Energy should be within 20% (resampling redistributes energy across more/fewer samples)
        var ratio = outputEnergy / inputEnergy;
        Assert.InRange(ratio, 0.8, 1.2);
    }

    // ── CalculateTargetLength ─────────────────────────────────────────────

    [Fact]
    public void CalculateTargetLength_44100To48000()
    {
        var len = HrirResampler.CalculateTargetLength(256, 44100, 48000);
        Assert.Equal(279, len);
    }

    [Fact]
    public void CalculateTargetLength_48000To44100()
    {
        var len = HrirResampler.CalculateTargetLength(256, 48000, 44100);
        Assert.Equal(235, len);
    }

    [Fact]
    public void CalculateTargetLength_SameRate()
    {
        var len = HrirResampler.CalculateTargetLength(256, 48000, 48000);
        Assert.Equal(256, len);
    }

    [Fact]
    public void CalculateTargetLength_ZeroInput_ReturnsZero()
    {
        var len = HrirResampler.CalculateTargetLength(0, 48000, 48000);
        Assert.Equal(0, len);
    }
}
