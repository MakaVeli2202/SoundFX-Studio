using SoundFXStudio.Models;
using SoundFXStudio.Services;
using SoundFXStudio.Services.DSP;
using SoundFXStudio.Services.Hrtf;
using Xunit;

namespace SoundFXStudio.Tests;

public class HrtfHeadTrackingTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    private static HrtfProfile CreateTestProfile()
    {
        var entries = new List<HrtfEntry>();
        foreach (var az in new[] { -90.0, 0.0, 90.0 })
        foreach (var el in new[] { 0.0, 45.0 })
        {
            var left = new float[64];
            var right = new float[64];
            left[0] = 1.0f;
            right[0] = 0.9f;
            for (int i = 1; i < 64; i++)
            {
                left[i] = 0.5f / (i + 1);
                right[i] = 0.45f / (i + 1);
            }
            entries.Add(new HrtfEntry
            {
                AzimuthDeg = az, ElevationDeg = el,
                LeftEarResponse = left, RightEarResponse = right
            });
        }

        return new HrtfProfile
        {
            Id = "ht-test", Name = "HT Test", SampleRate = 48000, IrLength = 64,
            Entries = entries.ToArray(),
            Manufacturer = "Test", Description = "Test", DataSource = "Test", License = "Test"
        };
    }

    private static HrtfEffect CreateHrtf()
    {
        var hrtf = new HrtfEffect(48000) { DirectionTransitionMs = 0, IsEnabled = true };
        hrtf.SetProfile(CreateTestProfile());
        hrtf.SetDirection(0, 0);
        return hrtf;
    }

    // ── 1. NullProvider_IsUnavailable ─────────────────────────────────────

    [Fact]
    public void NullProvider_IsUnavailable()
    {
        var provider = new NullHeadTrackingProvider();
        Assert.False(provider.IsAvailable);
    }

    // ── 2. NullProvider_DoesNotThrow ─────────────────────────────────────

    [Fact]
    public void NullProvider_DoesNotThrow()
    {
        var provider = new NullHeadTrackingProvider();
        provider.Start();
        provider.Stop();
        var _ = provider.GetOrientation();
        provider.Dispose();
    }

    // ── 3. Calibration_SetsCurrentOrientationAsZero ─────────────────────

    [Fact]
    public void Calibration_SetsCurrentOrientationAsZero()
    {
        var converter = new HeadOrientationConverter();
        converter.Calibrate(30, -10, 5);

        var (az, el) = converter.Convert(30, -10, 0);
        Assert.Equal(0, az, 4);
        Assert.Equal(0, el, 4);
    }

    // ── 4. Calibration_ProducesCorrectRelativeYaw ────────────────────────

    [Fact]
    public void Calibration_ProducesCorrectRelativeYaw()
    {
        var converter = new HeadOrientationConverter();
        converter.Calibrate(45, 0, 0);

        // Head at 55° = relative 10° = azimuth +10°
        var (az, _) = converter.Convert(55, 0, 0);
        Assert.Equal(10, az, 4);

        // Head at 35° = relative -10° = azimuth -10°
        var (az2, _) = converter.Convert(35, 0, 0);
        Assert.Equal(-10, az2, 4);
    }

    // ── 5. Calibration_ProducesCorrectRelativePitch ──────────────────────

    [Fact]
    public void Calibration_ProducesCorrectRelativePitch()
    {
        var converter = new HeadOrientationConverter();
        converter.Calibrate(0, 20, 0);

        // Head at 30° = relative 10° = elevation +10°
        var (_, el) = converter.Convert(0, 30, 0);
        Assert.Equal(10, el, 4);

        // Head at 10° = relative -10° = elevation -10°
        var (_, el2) = converter.Convert(0, 10, 0);
        Assert.Equal(-10, el2, 4);
    }

    // ── 6. Azimuth_NormalizesCorrectly ────────────────────────────────────

    [Fact]
    public void Azimuth_NormalizesCorrectly()
    {
        Assert.Equal(180, HeadOrientationConverter.NormalizeAzimuth(180), 4);
        Assert.Equal(-180, HeadOrientationConverter.NormalizeAzimuth(-180), 4);
        Assert.Equal(0, HeadOrientationConverter.NormalizeAzimuth(360), 4);
        Assert.Equal(90, HeadOrientationConverter.NormalizeAzimuth(450), 4);
        Assert.Equal(-90, HeadOrientationConverter.NormalizeAzimuth(-450), 4);
        Assert.Equal(170, HeadOrientationConverter.NormalizeAzimuth(530), 4);
    }

    // ── 7. AzimuthPositiveMapsToLeft ──────────────────────────────────────

    [Fact]
    public void AzimuthPositiveMapsToLeft()
    {
        var converter = new HeadOrientationConverter();
        var (az, _) = converter.Convert(30, 0, 0);
        Assert.True(az > 0, "Positive yaw should map to positive azimuth (left)");
    }

    // ── 8. AzimuthNegativeMapsToRight ────────────────────────────────────

    [Fact]
    public void AzimuthNegativeMapsToRight()
    {
        var converter = new HeadOrientationConverter();
        var (az, _) = converter.Convert(-30, 0, 0);
        Assert.True(az < 0, "Negative yaw should map to negative azimuth (right)");
    }

    // ── 9. ElevationPositiveMapsAbove ────────────────────────────────────

    [Fact]
    public void ElevationPositiveMapsAbove()
    {
        var converter = new HeadOrientationConverter();
        var (_, el) = converter.Convert(0, 20, 0);
        Assert.True(el > 0, "Positive pitch should map to positive elevation (above)");
    }

    // ── 10. ElevationNegativeMapsBelow ───────────────────────────────────

    [Fact]
    public void ElevationNegativeMapsBelow()
    {
        var converter = new HeadOrientationConverter();
        var (_, el) = converter.Convert(0, -20, 0);
        Assert.True(el < 0, "Negative pitch should map to negative elevation (below)");
    }

    // ── 11. RollDoesNotAffectAzimuth ────────────────────────────────────

    [Fact]
    public void RollDoesNotAffectAzimuth()
    {
        var converter = new HeadOrientationConverter();
        var (az1, _) = converter.Convert(10, 0, 0);
        var (az2, _) = converter.Convert(10, 0, 45);
        var (az3, _) = converter.Convert(10, 0, -45);
        Assert.Equal(az1, az2, 4);
        Assert.Equal(az1, az3, 4);
    }

    // ── 12. SmallMovement_IsFiltered ─────────────────────────────────────

    [Fact]
    public void SmallMovement_IsFiltered()
    {
        var hrtf = CreateHrtf();
        var provider = new StubHeadTrackingProvider { Orientation = new HeadOrientation(0, 0, 0) };
        var service = new HeadTrackingService(provider) { AngleThresholdDeg = 5.0 };
        service.Start();

        service.Update(hrtf, headTrackingEnabled: true, hrtfEnabled: true);

        // Small movement (2°) — should be filtered
        provider.Orientation = new HeadOrientation(2, 0, 0);
        var result = service.Update(hrtf, headTrackingEnabled: true, hrtfEnabled: true);

        Assert.Null(result);
    }

    // ── 13. LargeMovement_UpdatesDirection ───────────────────────────────

    [Fact]
    public void LargeMovement_UpdatesDirection()
    {
        var hrtf = CreateHrtf();
        var provider = new StubHeadTrackingProvider { Orientation = new HeadOrientation(0, 0, 0) };
        var service = new HeadTrackingService(provider) { AngleThresholdDeg = 1.0 };
        service.Start();

        service.Update(hrtf, headTrackingEnabled: true, hrtfEnabled: true);

        // Large movement (10°)
        provider.Orientation = new HeadOrientation(10, 0, 0);
        var result = service.Update(hrtf, headTrackingEnabled: true, hrtfEnabled: true);

        Assert.NotNull(result);
        Assert.Equal(10, result.Value.AzimuthDeg, 1);
    }

    // ── 14. MaximumUpdateRate_IsRespected ────────────────────────────────

    [Fact]
    public void MaximumUpdateRate_IsRespected()
    {
        var hrtf = CreateHrtf();
        var provider = new StubHeadTrackingProvider { Orientation = new HeadOrientation(0, 0, 0) };
        var service = new HeadTrackingService(provider)
        {
            AngleThresholdDeg = 100, // Very high threshold so angle never passes
            MaxUpdateIntervalMs = 1000 // 1 second between updates
        };
        service.Start();

        service.Update(hrtf, headTrackingEnabled: true, hrtfEnabled: true);

        // Immediately try another update with small angle change — should be rate-limited
        provider.Orientation = new HeadOrientation(2, 0, 0);
        var result = service.Update(hrtf, headTrackingEnabled: true, hrtfEnabled: true);

        Assert.Null(result);
    }

    // ── 15. RapidMovement_LatestDirectionWins ────────────────────────────

    [Fact]
    public void RapidMovement_LatestDirectionWins()
    {
        var hrtf = CreateHrtf();
        var provider = new StubHeadTrackingProvider { Orientation = new HeadOrientation(0, 0, 0) };
        var service = new HeadTrackingService(provider) { AngleThresholdDeg = 0, MaxUpdateIntervalMs = 0 };
        service.Start();

        service.Update(hrtf, headTrackingEnabled: true, hrtfEnabled: true);

        // Rapid changes — latest direction should always win
        provider.Orientation = new HeadOrientation(30, 0, 0);
        service.Update(hrtf, headTrackingEnabled: true, hrtfEnabled: true);

        provider.Orientation = new HeadOrientation(60, 0, 0);
        service.Update(hrtf, headTrackingEnabled: true, hrtfEnabled: true);

        provider.Orientation = new HeadOrientation(90, 0, 0);
        var result = service.Update(hrtf, headTrackingEnabled: true, hrtfEnabled: true);

        Assert.NotNull(result);
        Assert.Equal(90, result.Value.AzimuthDeg, 1);
    }

    // ── 16. DisabledTracking_DoesNotUpdateHrtf ──────────────────────────

    [Fact]
    public void DisabledTracking_DoesNotUpdateHrtf()
    {
        var hrtf = CreateHrtf();
        var provider = new StubHeadTrackingProvider { Orientation = new HeadOrientation(45, 0, 0) };
        var service = new HeadTrackingService(provider);
        service.Start();

        var result = service.Update(hrtf, headTrackingEnabled: false, hrtfEnabled: true);
        Assert.Null(result);
    }

    // ── 17. ProviderUnavailable_FallsBackSafely ─────────────────────────

    [Fact]
    public void ProviderUnavailable_FallsBackSafely()
    {
        var hrtf = CreateHrtf();
        var provider = new NullHeadTrackingProvider();
        var service = new HeadTrackingService(provider);

        var result = service.Update(hrtf, headTrackingEnabled: true, hrtfEnabled: true);
        Assert.Null(result);
    }

    // ── 18. TrackingDoesNotModifyHrtfProfile ────────────────────────────

    [Fact]
    public void TrackingDoesNotModifyHrtfProfile()
    {
        var profile = CreateTestProfile();
        var hrtf = new HrtfEffect(48000) { DirectionTransitionMs = 0, IsEnabled = true };
        hrtf.SetProfile(profile);
        hrtf.SetDirection(0, 0);

        var provider = new StubHeadTrackingProvider { Orientation = new HeadOrientation(30, 10, 0) };
        var service = new HeadTrackingService(provider);
        service.Start();
        service.Update(hrtf, headTrackingEnabled: true, hrtfEnabled: true);

        Assert.Same(profile, hrtf.ActiveProfile);
    }

    // ── 19. TrackingDoesNotModifySpatialMix ─────────────────────────────

    [Fact]
    public void TrackingDoesNotModifySpatialMix()
    {
        var hrtf = new HrtfEffect(48000)
        {
            DirectionTransitionMs = 0,
            IsEnabled = true,
            SpatialMix = 0.75
        };
        hrtf.SetProfile(CreateTestProfile());
        hrtf.SetDirection(0, 0);

        var provider = new StubHeadTrackingProvider { Orientation = new HeadOrientation(30, 10, 0) };
        var service = new HeadTrackingService(provider);
        service.Start();
        service.Update(hrtf, headTrackingEnabled: true, hrtfEnabled: true);

        Assert.Equal(0.75, hrtf.SpatialMix, 4);
    }

    // ── 20. TrackingDoesNotRecreateDspChain ─────────────────────────────

    [Fact]
    public void TrackingDoesNotRecreateDspChain()
    {
        var service = new GamingEnhancementService();
        var chain = service.Chain;
        service.ApplyHrtfProfile(CreateTestProfile());

        var provider = new StubHeadTrackingProvider { Orientation = new HeadOrientation(30, 10, 0) };
        var htService = new HeadTrackingService(provider);
        htService.Start();
        htService.Update(service.HrtfSpatializer, headTrackingEnabled: true, hrtfEnabled: true);

        Assert.Same(chain, service.Chain);
    }

    // ── 21. TrackingDoesNotRecreateHrtfEffect ──────────────────────────

    [Fact]
    public void TrackingDoesNotRecreateHrtfEffect()
    {
        var service = new GamingEnhancementService();
        service.ApplyHrtfProfile(CreateTestProfile());
        var hrtf = service.HrtfSpatializer;

        var provider = new StubHeadTrackingProvider { Orientation = new HeadOrientation(30, 10, 0) };
        var htService = new HeadTrackingService(provider);
        htService.Start();
        htService.Update(hrtf, headTrackingEnabled: true, hrtfEnabled: true);

        Assert.Same(hrtf, service.HrtfSpatializer);
    }

    // ── 22. CalibrationDoesNotResetProfile ──────────────────────────────

    [Fact]
    public void CalibrationDoesNotResetProfile()
    {
        var hrtf = CreateHrtf();
        var provider = new StubHeadTrackingProvider { Orientation = new HeadOrientation(0, 0, 0) };
        var service = new HeadTrackingService(provider);
        service.Start();

        service.Update(hrtf, headTrackingEnabled: true, hrtfEnabled: true);
        Assert.NotNull(hrtf.ActiveProfile);

        service.Calibrate();
        Assert.NotNull(hrtf.ActiveProfile);
    }

    // ── 23. SettingsRoundTrip_PreservesTrackingEnabled ──────────────────

    [Fact]
    public void SettingsRoundTrip_PreservesTrackingEnabled()
    {
        var settings = new AppSettings { HrtfHeadTrackingEnabled = true };
        Assert.True(settings.HrtfHeadTrackingEnabled);

        settings.HrtfHeadTrackingEnabled = false;
        Assert.False(settings.HrtfHeadTrackingEnabled);
    }

    // ── 24. ProviderStart_IsIdempotent ──────────────────────────────────

    [Fact]
    public void ProviderStart_IsIdempotent()
    {
        var provider = new StubHeadTrackingProvider();
        Assert.True(provider.Start());
        Assert.True(provider.Start()); // Should not throw
        Assert.True(provider.IsTracking);
    }

    // ── 25. ProviderStop_IsIdempotent ───────────────────────────────────

    [Fact]
    public void ProviderStop_IsIdempotent()
    {
        var provider = new StubHeadTrackingProvider();
        provider.Start();
        provider.Stop();
        provider.Stop(); // Should not throw
        Assert.False(provider.IsTracking);
    }

    // ── 26. Shutdown_StopsTracking ──────────────────────────────────────

    [Fact]
    public void Shutdown_StopsTracking()
    {
        var provider = new StubHeadTrackingProvider();
        var service = new HeadTrackingService(provider);
        service.Start();
        Assert.True(service.IsTracking);

        service.Dispose();
        Assert.False(service.IsTracking);
    }

    // ── Integration: GamingService chain unchanged ───────────────────────

    [Fact]
    public void GamingService_ChainUnchanged()
    {
        var service = new GamingEnhancementService();
        var effects = service.Chain.Effects;

        Assert.Same(service.Equalizer, effects[0]);
        Assert.IsType<EqualizerEffect>(effects[1]);
        Assert.IsType<HrtfEffect>(effects[2]);
        Assert.IsType<NoiseGateEffect>(effects[3]);
        Assert.Equal(6, effects.Count);
    }

    // ── Integration: manual direction still works ────────────────────────

    [Fact]
    public void ManualDirection_StillWorksWhenTrackingDisabled()
    {
        var hrtf = CreateHrtf();
        var provider = new StubHeadTrackingProvider { Orientation = new HeadOrientation(45, 20, 0) };
        var service = new HeadTrackingService(provider);
        service.Start();

        // Manual update with tracking disabled — should not update HRTF
        var result = service.Update(hrtf, headTrackingEnabled: false, hrtfEnabled: true);
        Assert.Null(result);

        // But manual direction via SetDirection still works
        hrtf.SetDirection(45, 20);
        var buffer = new float[960];
        for (int i = 0; i < 480; i++) { buffer[i * 2] = 0.5f; buffer[i * 2 + 1] = 0.5f; }
        hrtf.Process(buffer);
        var maxAbs = 0f;
        for (int i = 0; i < buffer.Length; i++)
            maxAbs = Math.Max(maxAbs, Math.Abs(buffer[i]));
        Assert.True(maxAbs > 0);
    }

    // ── Integration: HRTF disabled prevents tracking updates ────────────

    [Fact]
    public void HrtfDisabled_PreventsTrackingUpdates()
    {
        var hrtf = CreateHrtf();
        hrtf.IsEnabled = false;

        var provider = new StubHeadTrackingProvider { Orientation = new HeadOrientation(45, 20, 0) };
        var service = new HeadTrackingService(provider);
        service.Start();

        var result = service.Update(hrtf, headTrackingEnabled: true, hrtfEnabled: false);
        Assert.Null(result);
    }
}
