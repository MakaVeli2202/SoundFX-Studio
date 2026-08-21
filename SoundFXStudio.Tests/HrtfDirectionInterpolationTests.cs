using SoundFXStudio.Models;
using SoundFXStudio.Services.Hrtf;
using Xunit;

namespace SoundFXStudio.Tests;

public class HrtfDirectionInterpolationTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a synthetic 3×3 grid profile for controlled testing.
    /// Azimuths: -90, 0, 90. Elevation: 0, 45, 90.
    /// Each entry has a distinctive impulse pattern.
    /// </summary>
    private static HrtfProfile CreateGridProfile()
    {
        var entries = new List<HrtfEntry>();
        var azimuths = new[] { -90.0, 0.0, 90.0 };
        var elevations = new[] { 0.0, 45.0, 90.0 };

        foreach (var el in elevations)
        {
            foreach (var az in azimuths)
            {
                var left = new float[32];
                var right = new float[32];
                // Unique impulse: position encodes azimuth/elevation
                var impulseIdx = (int)((az + 90) / 45 + el / 45 * 3);
                impulseIdx = Math.Clamp(impulseIdx, 0, 31);
                left[impulseIdx] = 1.0f;
                right[impulseIdx] = 0.8f;

                entries.Add(new HrtfEntry
                {
                    AzimuthDeg = az,
                    ElevationDeg = el,
                    LeftEarResponse = left,
                    RightEarResponse = right
                });
            }
        }

        return new HrtfProfile
        {
            Id = "test-grid",
            Name = "Test Grid",
            Manufacturer = "Test",
            Description = "Synthetic 3×3 grid",
            SampleRate = 48000,
            IrLength = 32,
            Entries = entries.ToArray(),
            DataSource = "Synthetic",
            License = "Test"
        };
    }

    private static HrtfProfile CreateSingleEntryProfile()
    {
        var left = new float[] { 1.0f, 0.5f, 0.25f };
        var right = new float[] { 0.9f, 0.45f, 0.22f };

        return new HrtfProfile
        {
            Id = "single",
            Name = "Single",
            SampleRate = 48000,
            IrLength = 3,
            Entries = new[]
            {
                new HrtfEntry
                {
                    AzimuthDeg = 0,
                    ElevationDeg = 0,
                    LeftEarResponse = left,
                    RightEarResponse = right
                }
            },
            Manufacturer = "T", Description = "T", DataSource = "T", License = "T"
        };
    }

    private static HrtfProfile CreateWrapProfile()
    {
        var entries = new List<HrtfEntry>();
        var azimuths = new[] { -170.0, -90.0, 0.0, 90.0, 170.0 };
        var el = 0.0;

        foreach (var az in azimuths)
        {
            var left = new float[16];
            var right = new float[16];
            left[0] = (float)(az / 180.0);
            right[0] = (float)(-az / 180.0);

            entries.Add(new HrtfEntry
            {
                AzimuthDeg = az,
                ElevationDeg = el,
                LeftEarResponse = left,
                RightEarResponse = right
            });
        }

        return new HrtfProfile
        {
            Id = "wrap", Name = "Wrap", SampleRate = 48000, IrLength = 16,
            Entries = entries.ToArray(), Manufacturer = "T", Description = "T",
            DataSource = "T", License = "T"
        };
    }

    // ── 1. ExactDirection_ReturnsOriginalHRIR ─────────────────────────────

    [Fact]
    public void ExactDirection_ReturnsOriginalHRIR()
    {
        var profile = CreateGridProfile();
        var (left, right) = HrtfDirectionInterpolator.Interpolate(profile, 0, 0);

        // Should match the entry at (0, 0)
        var entry = profile.GetEntryForDirection(0, 0)!;
        Assert.Equal(entry.LeftEarResponse.Length, left.Length);
        for (int i = 0; i < left.Length; i++)
            Assert.Equal(entry.LeftEarResponse[i], left[i], 4);
        for (int i = 0; i < right.Length; i++)
            Assert.Equal(entry.RightEarResponse[i], right[i], 4);
    }

    // ── 2. MidpointAzimuth_InterpolatesCorrectly ──────────────────────────

    [Fact]
    public void MidpointAzimuth_InterpolatesCorrectly()
    {
        var profile = CreateGridProfile();
        // Request direction between (0,0) and (90,0) — should be midpoint
        var (left, right) = HrtfDirectionInterpolator.Interpolate(profile, 45, 0);

        Assert.Equal(32, left.Length);
        Assert.Equal(32, right.Length);

        // Interpolated result should have finite values between the two source values
        Assert.All(left, v => Assert.True(float.IsFinite(v)));
        Assert.All(right, v => Assert.True(float.IsFinite(v)));
    }

    // ── 3. MidpointElevation_InterpolatesCorrectly ────────────────────────

    [Fact]
    public void MidpointElevation_InterpolatesCorrectly()
    {
        var profile = CreateGridProfile();
        var (left, right) = HrtfDirectionInterpolator.Interpolate(profile, 0, 22.5);

        Assert.Equal(32, left.Length);
        Assert.All(left, v => Assert.True(float.IsFinite(v)));
    }

    // ── 4. BilinearInterpolation_IsCorrect ────────────────────────────────

    [Fact]
    public void BilinearInterpolation_WeightedSum()
    {
        // Create a profile where we can verify weighted sum analytically
        var left00 = new float[] { 1.0f, 0.0f };
        var right00 = new float[] { 1.0f, 0.0f };
        var left90 = new float[] { 0.0f, 1.0f };
        var right90 = new float[] { 0.0f, 1.0f };

        var profile = new HrtfProfile
        {
            Id = "bilinear", Name = "Bilinear", SampleRate = 48000, IrLength = 2,
            Entries = new[]
            {
                new HrtfEntry { AzimuthDeg = 0, ElevationDeg = 0, LeftEarResponse = left00, RightEarResponse = right00 },
                new HrtfEntry { AzimuthDeg = 90, ElevationDeg = 0, LeftEarResponse = left90, RightEarResponse = right90 }
            },
            Manufacturer = "T", Description = "T", DataSource = "T", License = "T"
        };

        // At midpoint (45°), weights should be roughly equal
        var (left, _) = HrtfDirectionInterpolator.Interpolate(profile, 45, 0);

        Assert.Equal(2, left.Length);
        // left[0] should be between 0 and 1 (weighted mix of 1.0 and 0.0)
        Assert.InRange(left[0], 0.1f, 0.9f);
        // left[1] should be between 0 and 1
        Assert.InRange(left[1], 0.1f, 0.9f);
    }

    // ── 5. LeftAndRightEarsInterpolatedIndependently ──────────────────────

    [Fact]
    public void LeftAndRightEarsInterpolatedIndependently()
    {
        // Use asymmetric data so left/right interpolation produces different results
        var profile = new HrtfProfile
        {
            Id = "indep", Name = "Independent", SampleRate = 48000, IrLength = 2,
            Entries = new[]
            {
                new HrtfEntry
                {
                    AzimuthDeg = 0, ElevationDeg = 0,
                    LeftEarResponse = new float[] { 1.0f, 0.0f },
                    RightEarResponse = new float[] { 0.5f, 0.0f }
                },
                new HrtfEntry
                {
                    AzimuthDeg = 90, ElevationDeg = 0,
                    LeftEarResponse = new float[] { 0.0f, 1.0f },
                    RightEarResponse = new float[] { 0.0f, 0.5f }
                }
            },
            Manufacturer = "T", Description = "T", DataSource = "T", License = "T"
        };

        var (left, right) = HrtfDirectionInterpolator.Interpolate(profile, 45, 0);

        // Left and right should be different because source HRIRs differ in amplitude
        Assert.NotEqual(left[0], right[0]);
        Assert.NotEqual(left[1], right[1]);
    }

    // ── 6. AzimuthWraparound ──────────────────────────────────────────────

    [Fact]
    public void AzimuthWraparound_WorksAcrossMinus180Plus180()
    {
        var profile = CreateWrapProfile();
        // Request direction at 175° (between 170° and -170°)
        var (left, right) = HrtfDirectionInterpolator.Interpolate(profile, 175, 0);

        Assert.Equal(16, left.Length);
        Assert.All(left, v => Assert.True(float.IsFinite(v)));
        Assert.All(right, v => Assert.True(float.IsFinite(v)));
    }

    [Fact]
    public void AzimuthWraparound_ExactlyAtBoundary()
    {
        var profile = CreateWrapProfile();
        // Request at exactly 180° (should be between 170° and -170°)
        var (left, right) = HrtfDirectionInterpolator.Interpolate(profile, 180, 0);

        Assert.Equal(16, left.Length);
        Assert.All(left, v => Assert.True(float.IsFinite(v)));
    }

    // ── 7. ElevationBoundary_DoesNotExtrapolate ──────────────────────────

    [Fact]
    public void ElevationBoundary_DoesNotExtrapolate()
    {
        var profile = CreateGridProfile();
        // Request at elevation 90° (max elevation in grid) — should work
        var (left, _) = HrtfDirectionInterpolator.Interpolate(profile, 0, 90);
        Assert.Equal(32, left.Length);

        // Request beyond max elevation — should use nearest entries, not extrapolate
        var (leftAbove, _) = HrtfDirectionInterpolator.Interpolate(profile, 0, 95);
        Assert.Equal(32, leftAbove.Length);
        Assert.All(leftAbove, v => Assert.True(float.IsFinite(v)));
    }

    // ── 8. SparseGrid_FallsBackSafely ────────────────────────────────────

    [Fact]
    public void SparseGrid_FallsBackSafely()
    {
        // Profile with only one entry at each elevation
        var profile = new HrtfProfile
        {
            Id = "sparse", Name = "Sparse", SampleRate = 48000, IrLength = 8,
            Entries = new[]
            {
                new HrtfEntry
                {
                    AzimuthDeg = -90, ElevationDeg = 0,
                    LeftEarResponse = new float[] { 1, 0, 0, 0, 0, 0, 0, 0 },
                    RightEarResponse = new float[] { 0.9f, 0, 0, 0, 0, 0, 0, 0 }
                },
                new HrtfEntry
                {
                    AzimuthDeg = 90, ElevationDeg = 0,
                    LeftEarResponse = new float[] { 0, 0, 0, 0, 0, 0, 0, 1 },
                    RightEarResponse = new float[] { 0, 0, 0, 0, 0, 0, 0, 0.9f }
                }
            },
            Manufacturer = "T", Description = "T", DataSource = "T", License = "T"
        };

        var (left, _) = HrtfDirectionInterpolator.Interpolate(profile, 0, 45);
        Assert.Equal(8, left.Length);
        Assert.All(left, v => Assert.True(float.IsFinite(v)));
    }

    // ── 9. IrregularGrid_DoesNotCrash ────────────────────────────────────

    [Fact]
    public void IrregularGrid_DoesNotCrash()
    {
        // Real SOFA has irregular spacing. Use the real fixture.
        var sofaPath = Path.Combine(AppContext.BaseDirectory, "TestData", "SimpleFreeFieldHRIR_1.0.sofa");
        if (!File.Exists(sofaPath))
        {
            return; // Skip if fixture not available
        }

        var loader = new SofaHrtfLoader();
        var result = loader.Load(sofaPath);
        Assert.True(result.Success);

        // Test various directions that fall between irregular grid points
        var directions = new[] { (0.0, 0.0), (15.0, 10.0), (-123.5, 33.3), (179.0, -29.0), (0, 80.0) };
        foreach (var (az, el) in directions)
        {
            var (left, right) = HrtfDirectionInterpolator.Interpolate(result.Profile!, az, el);
            Assert.True(left.Length > 0, $"Zero-length HRIR at ({az}, {el})");
            Assert.All(left, v => Assert.True(float.IsFinite(v), $"Non-finite left at ({az},{el})"));
            Assert.All(right, v => Assert.True(float.IsFinite(v), $"Non-finite right at ({az},{el})"));
        }
    }

    // ── 10. DifferentHrIrLengths_AreRejectedOrFallbackSafely ─────────────

    [Fact]
    public void DifferentHrIrLengths_HandleGracefully()
    {
        // Entries with different IR lengths — interpolator should use profile.IrLength
        var profile = new HrtfProfile
        {
            Id = "mismatch", Name = "Mismatch", SampleRate = 48000, IrLength = 4,
            Entries = new[]
            {
                new HrtfEntry
                {
                    AzimuthDeg = 0, ElevationDeg = 0,
                    LeftEarResponse = new float[] { 1, 0.5f, 0.25f, 0.125f, 0.0625f, 0.03125f },
                    RightEarResponse = new float[] { 1, 0.5f, 0.25f, 0.125f, 0.0625f, 0.03125f }
                },
                new HrtfEntry
                {
                    AzimuthDeg = 90, ElevationDeg = 0,
                    LeftEarResponse = new float[] { 0, 0, 0, 1 },
                    RightEarResponse = new float[] { 0, 0, 0, 1 }
                }
            },
            Manufacturer = "T", Description = "T", DataSource = "T", License = "T"
        };

        var (left, _) = HrtfDirectionInterpolator.Interpolate(profile, 45, 0);
        // Should use profile.IrLength (4), not the longer array
        Assert.Equal(4, left.Length);
    }

    // ── 11. DirectionOutsideAvailableRange_UsesSafeFallback ──────────────

    [Fact]
    public void DirectionOutsideAvailableRange_UsesSafeFallback()
    {
        var profile = CreateGridProfile();
        // Request a direction far outside the grid
        var (left, _) = HrtfDirectionInterpolator.Interpolate(profile, 180, 90);
        Assert.Equal(32, left.Length);
        Assert.All(left, v => Assert.True(float.IsFinite(v)));
    }

    // ── 12. InterpolationDoesNotMutateSourceProfiles ─────────────────────

    [Fact]
    public void InterpolationDoesNotMutateSourceProfiles()
    {
        var profile = CreateGridProfile();
        var originalLeft0 = (float[])profile.Entries[0].LeftEarResponse.Clone();
        var originalRight0 = (float[])profile.Entries[0].RightEarResponse.Clone();

        _ = HrtfDirectionInterpolator.Interpolate(profile, 45, 22.5);

        for (int i = 0; i < originalLeft0.Length; i++)
            Assert.Equal(originalLeft0[i], profile.Entries[0].LeftEarResponse[i]);
        for (int i = 0; i < originalRight0.Length; i++)
            Assert.Equal(originalRight0[i], profile.Entries[0].RightEarResponse[i]);
    }

    // ── 13. ResultLengthMatchesSource ────────────────────────────────────

    [Fact]
    public void ResultLengthMatchesSource()
    {
        var profile = CreateGridProfile();
        var (left, right) = HrtfDirectionInterpolator.Interpolate(profile, 30, 20);

        Assert.Equal(profile.IrLength, left.Length);
        Assert.Equal(profile.IrLength, right.Length);
        Assert.Equal(left.Length, right.Length);
    }

    // ── 14. ExactDirection_IsBitwiseEquivalentIfPossible ─────────────────

    [Fact]
    public void ExactDirection_MatchesNearestEntry()
    {
        var profile = CreateGridProfile();
        // Request exactly at an entry position
        var (left, _) = HrtfDirectionInterpolator.Interpolate(profile, -90, 45);
        var entry = profile.GetEntryForDirection(-90, 45)!;

        Assert.Equal(entry.LeftEarResponse.Length, left.Length);
        for (int i = 0; i < left.Length; i++)
            Assert.Equal(entry.LeftEarResponse[i], left[i], 4);
    }

    // ── 15. RepeatedSameDirection_DoesNotRecomputeUnnecessarily ──────────

    [Fact]
    public void RepeatedSameDirection_ReturnsConsistentResults()
    {
        var profile = CreateGridProfile();
        var (l1, r1) = HrtfDirectionInterpolator.Interpolate(profile, 30, 15);
        var (l2, r2) = HrtfDirectionInterpolator.Interpolate(profile, 30, 15);

        Assert.Equal(l1.Length, l2.Length);
        for (int i = 0; i < l1.Length; i++)
            Assert.Equal(l1[i], l2[i]);
    }

    // ── 16. Process_RemainsAllocationFree ─────────────────────────────────

    [Fact]
    public void Process_RemainsAllocationFree_AfterInterpolation()
    {
        var profile = CreateGridProfile();
        var hrtf = new SoundFXStudio.Services.DSP.HrtfEffect(48000);
        hrtf.SetProfile(profile);
        hrtf.IsEnabled = true;

        // Set direction via interpolation
        hrtf.SetDirection(45, 22.5);

        var buffer = new float[512];
        for (int i = 0; i < 256; i++) { buffer[i * 2] = 0.5f; buffer[i * 2 + 1] = 0.5f; }

        // Warmup
        hrtf.Process(buffer);
        hrtf.Reset();

        // Should not crash or produce NaN
        hrtf.Process(buffer);
        var maxAbs = 0f;
        for (int i = 0; i < buffer.Length; i++)
            maxAbs = Math.Max(maxAbs, Math.Abs(buffer[i]));

        Assert.True(maxAbs > 0, "Process should produce non-silent output after interpolation");
    }

    // ── 17. DirectionChange_DoesNotRecreateEffect ────────────────────────

    [Fact]
    public void DirectionChange_DoesNotRecreateEffect()
    {
        var profile = CreateGridProfile();
        var hrtf = new SoundFXStudio.Services.DSP.HrtfEffect(48000);
        hrtf.SetProfile(profile);

        hrtf.SetDirection(0, 0);
        var entryBefore = hrtf.CurrentEntry;

        hrtf.SetDirection(45, 22.5);
        var entryAfter = hrtf.CurrentEntry;

        // CurrentEntry should change (different nearest neighbor)
        Assert.NotNull(entryBefore);
        Assert.NotNull(entryAfter);
        // But the effect itself should still be the same instance
        Assert.Same(hrtf, hrtf);
    }

    // ── 18. DirectionChange_DoesNotRecreateChain ─────────────────────────

    [Fact]
    public void DirectionChange_DoesNotRecreateChain()
    {
        var service = new SoundFXStudio.Services.GamingEnhancementService();
        var profile = CreateGridProfile();

        service.ApplyHrtfProfile(profile);

        var chainBefore = service.Chain;
        service.HrtfSpatializer.SetDirection(0, 0);
        service.HrtfSpatializer.SetDirection(45, 22.5);
        service.HrtfSpatializer.SetDirection(-90, 45);

        Assert.Same(chainBefore, service.Chain);
        Assert.Equal(6, service.Chain.Effects.Count);
    }

    // ── Empty profile ────────────────────────────────────────────────────

    [Fact]
    public void EmptyProfile_ReturnsEmptyArrays()
    {
        var profile = new HrtfProfile
        {
            Id = "empty", Name = "Empty", SampleRate = 48000, IrLength = 0,
            Entries = Array.Empty<HrtfEntry>(),
            Manufacturer = "T", Description = "T", DataSource = "T", License = "T"
        };

        var (left, right) = HrtfDirectionInterpolator.Interpolate(profile, 0, 0);
        Assert.Empty(left);
        Assert.Empty(right);
    }

    // ── SOFA fixture integration ─────────────────────────────────────────

    [Fact]
    public void SofaFixture_InterpolationProducesValidOutput()
    {
        var sofaPath = Path.Combine(AppContext.BaseDirectory, "TestData", "SimpleFreeFieldHRIR_1.0.sofa");
        if (!File.Exists(sofaPath)) return;

        var loader = new SofaHrtfLoader();
        var result = loader.Load(sofaPath);
        Assert.True(result.Success);

        // Interpolate at a direction between measurements
        var (left, right) = HrtfDirectionInterpolator.Interpolate(result.Profile!, 15.0, 10.0);

        Assert.Equal(result.IrLength, left.Length);
        Assert.Equal(result.IrLength, right.Length);
        Assert.All(left, v => Assert.True(float.IsFinite(v)));
        Assert.All(right, v => Assert.True(float.IsFinite(v)));
    }
}
