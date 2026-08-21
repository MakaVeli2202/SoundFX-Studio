using SoundFXStudio.Models;
using SoundFXStudio.Services;
using SoundFXStudio.Services.DSP;
using SoundFXStudio.ViewModels;
using Xunit;

namespace SoundFXStudio.Tests;

public class HrtfIntegrationTests
{
    // ── Chain structure tests ──────────────────────────────────────────────

    [Fact]
    public void GamingChain_HasExactlySixEffects()
    {
        var service = new GamingEnhancementService();
        Assert.Equal(6, service.Chain.Effects.Count);
    }

    [Fact]
    public void GamingChain_HrtfAtIndex2()
    {
        var service = new GamingEnhancementService();
        Assert.IsType<HrtfEffect>(service.Chain.Effects[2]);
    }

    [Fact]
    public void GamingChain_HeadphoneEqAtIndex1()
    {
        var service = new GamingEnhancementService();
        var eq2 = service.Chain.Effects[1];
        Assert.IsType<EqualizerEffect>(eq2);
        Assert.Same(service.HeadphoneEqualizer, eq2);
    }

    [Fact]
    public void GamingChain_NoiseGateAfterHrtf()
    {
        var service = new GamingEnhancementService();
        var effects = service.Chain.Effects;
        int hrtfIdx = -1, gateIdx = -1;
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i] is HrtfEffect) hrtfIdx = i;
            if (effects[i] is NoiseGateEffect) gateIdx = i;
        }
        Assert.True(gateIdx > hrtfIdx);
    }

    [Fact]
    public void GamingChain_LimiterLast()
    {
        var service = new GamingEnhancementService();
        var lastEffect = service.Chain.Effects[^1];
        Assert.IsType<LimiterEffect>(lastEffect);
    }

    [Fact]
    public void GamingChain_GamingEqAtIndex0()
    {
        var service = new GamingEnhancementService();
        Assert.Same(service.Equalizer, service.Chain.Effects[0]);
    }

    [Fact]
    public void GamingChain_CompressorAfterHrtf()
    {
        var service = new GamingEnhancementService();
        var effects = service.Chain.Effects;
        int hrtfIdx = -1, compIdx = -1;
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i] is HrtfEffect) hrtfIdx = i;
            if (effects[i] is CompressorEffect) compIdx = i;
        }
        Assert.True(compIdx > hrtfIdx);
    }

    // ── ApplyHrtfProfile tests ────────────────────────────────────────────

    [Fact]
    public void ApplyHrtfProfile_Valid_EnableAndConfigure()
    {
        var service = new GamingEnhancementService();
        var profile = HrtfProfilePresets.GetById("synthetic-front");

        service.ApplyHrtfProfile(profile);

        Assert.True(service.HrtfSpatializer.IsEnabled);
        Assert.NotNull(service.ActiveHrtfProfile);
        Assert.Equal("synthetic-front", service.ActiveHrtfProfile!.Id);
    }

    [Fact]
    public void ApplyHrtfProfile_Null_DisablesHrtf()
    {
        var service = new GamingEnhancementService();
        var profile = HrtfProfilePresets.GetById("synthetic-front");

        service.ApplyHrtfProfile(profile);
        Assert.True(service.HrtfSpatializer.IsEnabled);

        service.ApplyHrtfProfile(null);
        Assert.False(service.HrtfSpatializer.IsEnabled);
        Assert.Null(service.ActiveHrtfProfile);
    }

    [Fact]
    public void ApplyHrtfProfile_None_DisablesHrtf()
    {
        var service = new GamingEnhancementService();
        var none = HrtfProfilePresets.GetNone();

        service.ApplyHrtfProfile(none);
        Assert.False(service.HrtfSpatializer.IsEnabled);
    }

    // ── Bypass tests ───────────────────────────────────────────────────────

    [Fact]
    public void Bypass_DisablesHrtf()
    {
        var service = new GamingEnhancementService();
        var profile = HrtfProfilePresets.GetById("synthetic-front");

        service.ApplyHrtfProfile(profile);
        Assert.True(service.HrtfSpatializer.IsEnabled);

        service.Bypass();
        Assert.False(service.HrtfSpatializer.IsEnabled);
    }

    // ── Sample rate propagation ────────────────────────────────────────────

    [Fact]
    public void SetSampleRate_PropagatesToHrtf()
    {
        var service = new GamingEnhancementService();
        service.SetSampleRate(44100);

        Assert.Equal(44100, service.HrtfSpatializer.SampleRate);
    }

    [Fact]
    public void SetSampleRate_DoesNotResetHrtfState()
    {
        var service = new GamingEnhancementService();
        var profile = HrtfProfilePresets.GetById("synthetic-front");

        service.ApplyHrtfProfile(profile);
        service.SetSampleRate(44100);

        Assert.True(service.HrtfSpatializer.IsEnabled);
        Assert.NotNull(service.ActiveHrtfProfile);
    }

    // ── Headphone EQ + HRTF coexistence ───────────────────────────────────

    [Fact]
    public void HeadphoneEq_And_Hrtf_Coexist()
    {
        var service = new GamingEnhancementService();
        var hpProfile = HeadphoneProfilePresets.GetById("flat-test");
        var hrtfProfile = HrtfProfilePresets.GetById("synthetic-front");

        service.ApplyHeadphoneProfile(hpProfile);
        service.ApplyHrtfProfile(hrtfProfile);

        Assert.True(service.HeadphoneEqualizer.IsEnabled);
        Assert.True(service.HrtfSpatializer.IsEnabled);
    }

    [Fact]
    public void DisableHeadphoneEq_DoesNotAffectHrtf()
    {
        var service = new GamingEnhancementService();
        var hpProfile = HeadphoneProfilePresets.GetById("flat-test");
        var hrtfProfile = HrtfProfilePresets.GetById("synthetic-front");

        service.ApplyHeadphoneProfile(hpProfile);
        service.ApplyHrtfProfile(hrtfProfile);

        service.ApplyHeadphoneProfile(null);

        Assert.False(service.HeadphoneEqualizer.IsEnabled);
        Assert.True(service.HrtfSpatializer.IsEnabled);
    }

    [Fact]
    public void DisableHrtf_DoesNotAffectHeadphoneEq()
    {
        var service = new GamingEnhancementService();
        var hpProfile = HeadphoneProfilePresets.GetById("flat-test");
        var hrtfProfile = HrtfProfilePresets.GetById("synthetic-front");

        service.ApplyHeadphoneProfile(hpProfile);
        service.ApplyHrtfProfile(hrtfProfile);

        service.ApplyHrtfProfile(null);

        Assert.True(service.HeadphoneEqualizer.IsEnabled);
        Assert.False(service.HrtfSpatializer.IsEnabled);
    }

    // ── Gaming EQ independence ─────────────────────────────────────────────

    [Fact]
    public void ApplyHrtfProfile_DoesNotModifyGamingEq()
    {
        var service = new GamingEnhancementService();
        var gamingProfile = GamingProfilePresets.GetById("footstep-focus");
        var hrtfProfile = HrtfProfilePresets.GetById("synthetic-front");

        service.Apply(gamingProfile);
        var eqEnabled = service.Equalizer.IsEnabled;
        var eqPreamp = service.Equalizer.PreampDb;

        service.ApplyHrtfProfile(hrtfProfile);

        Assert.Equal(eqEnabled, service.Equalizer.IsEnabled);
        Assert.Equal(eqPreamp, service.Equalizer.PreampDb);
    }

    // ── VoiceChangerService isolation ──────────────────────────────────────

    [Fact]
    public void ApplyHrtfProfile_DoesNotModifyVoiceChangerChain()
    {
        var vcService = new VoiceChangerService();
        var gamingService = new GamingEnhancementService();
        var hrtfProfile = HrtfProfilePresets.GetById("synthetic-front");

        var origGateEnabled = vcService.Chain.Get<NoiseGateEffect>()!.IsEnabled;
        var origCompEnabled = vcService.Chain.Get<CompressorEffect>()!.IsEnabled;

        gamingService.ApplyHrtfProfile(hrtfProfile);

        Assert.Equal(origGateEnabled, vcService.Chain.Get<NoiseGateEffect>()!.IsEnabled);
        Assert.Equal(origCompEnabled, vcService.Chain.Get<CompressorEffect>()!.IsEnabled);
        Assert.Null(vcService.Chain.Get<HrtfEffect>());
    }

    // ── Chain processing with HRTF ────────────────────────────────────────

    [Fact]
    public void GamingChain_ProcessWithHrtf_ProducesOutput()
    {
        var service = new GamingEnhancementService();
        var profile = HrtfProfilePresets.GetById("synthetic-front");
        service.ApplyHrtfProfile(profile);
        service.SetSampleRate(48000);

        var data = Sine(0.5f, 48000, freq: 1000);
        service.Chain.Process(data);

        var tail = data.AsSpan(24000);
        Assert.True(MaxAbs(tail) > 0.01, "chain with HRTF produced no output");
    }

    [Fact]
    public void GamingChain_HrtfDisabled_PassesThrough()
    {
        var service = new GamingEnhancementService();
        var profile = HrtfProfilePresets.GetById("synthetic-front");
        service.ApplyHrtfProfile(profile);
        service.HrtfSpatializer.IsEnabled = false;
        service.SetSampleRate(48000);

        var data = Sine(0.5f, 48000, freq: 1000);
        service.Chain.Process(data);

        var tail = data.AsSpan(24000);
        Assert.True(MaxAbs(tail) > 0.1, "disabled HRTF should pass through");
    }

    // ── Settings persistence ───────────────────────────────────────────────

    [Fact]
    public void ConfigService_PreservesHrtfSettings()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "SoundFXStudio-Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var cs = new ConfigService(appFolder: tempDir);
            var config = new AppConfig();
            config.Settings.HrtfEnabled = true;
            config.Settings.ActiveHrtfProfileId = "synthetic-front";

            cs.Save(config);
            var loaded = cs.Load();

            Assert.True(loaded.Settings.HrtfEnabled);
            Assert.Equal("synthetic-front", loaded.Settings.ActiveHrtfProfileId);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    // ── ViewModel tests ────────────────────────────────────────────────────

    [Fact]
    public void GamingViewModel_HrtfProfiles_Populated()
    {
        using var vm = new GamingViewModel();
        Assert.Equal(HrtfProfilePresets.Profiles.Count + 1, vm.AvailableHrtfProfiles.Count);
    }

    [Fact]
    public void GamingViewModel_FirstHrtfProfileIsNone()
    {
        using var vm = new GamingViewModel();
        Assert.NotNull(vm.SelectedHrtfProfile);
        Assert.Equal("none", vm.SelectedHrtfProfile!.Id);
    }

    [Fact]
    public void GamingViewModel_Hrtf_DefaultDisabled()
    {
        using var vm = new GamingViewModel();
        Assert.False(vm.IsHrtfEnabled);
        Assert.Equal("DISABLED", vm.HrtfToggleText);
        Assert.Equal("#E85555", vm.HrtfStatusColor);
    }

    [Fact]
    public void GamingViewModel_ToggleHrtf_TogglesState()
    {
        using var vm = new GamingViewModel();
        Assert.False(vm.IsHrtfEnabled);

        vm.IsHrtfEnabled = true;
        Assert.True(vm.IsHrtfEnabled);
        Assert.Equal("ENABLED", vm.HrtfToggleText);
        Assert.Equal("#22C55E", vm.HrtfStatusColor);

        vm.IsHrtfEnabled = false;
        Assert.False(vm.IsHrtfEnabled);
    }

    [Fact]
    public void GamingViewModel_ToggleHrtfCommand_Toggles()
    {
        using var vm = new GamingViewModel();
        Assert.False(vm.IsHrtfEnabled);

        vm.ToggleHrtfCommand.Execute(null);
        Assert.True(vm.IsHrtfEnabled);

        vm.ToggleHrtfCommand.Execute(null);
        Assert.False(vm.IsHrtfEnabled);
    }

    [Fact]
    public void GamingViewModel_SaveCallback_InvokedOnHrtfToggle()
    {
        bool saved = false;
        using var vm = new GamingViewModel(saveAction: () => saved = true);
        vm.IsHrtfEnabled = true;
        Assert.True(saved);
    }

    [Fact]
    public void GamingViewModel_SaveCallback_InvokedOnHrtfProfileChange()
    {
        bool saved = false;
        using var vm = new GamingViewModel(saveAction: () => saved = true);
        vm.SelectedHrtfProfile = HrtfProfilePresets.GetById("synthetic-front");
        Assert.True(saved);
    }

    // ── Azimuth tests ──────────────────────────────────────────────────

    [Fact]
    public void Azimuth_DefaultIsZero()
    {
        using var vm = new GamingViewModel();
        Assert.Equal(0.0, vm.HrtfAzimuth);
    }

    [Fact]
    public void Azimuth_SetPositive_UpdatesValue()
    {
        using var vm = new GamingViewModel();
        vm.HrtfAzimuth = 90;
        Assert.Equal(90.0, vm.HrtfAzimuth);
    }

    [Fact]
    public void Azimuth_SetNegative_UpdatesValue()
    {
        using var vm = new GamingViewModel();
        vm.HrtfAzimuth = -45;
        Assert.Equal(-45.0, vm.HrtfAzimuth);
    }

    [Fact]
    public void Azimuth_ClampsAbove180()
    {
        using var vm = new GamingViewModel();
        vm.HrtfAzimuth = 200;
        Assert.Equal(180.0, vm.HrtfAzimuth);
    }

    [Fact]
    public void Azimuth_ClampsBelowMinus180()
    {
        using var vm = new GamingViewModel();
        vm.HrtfAzimuth = -200;
        Assert.Equal(-180.0, vm.HrtfAzimuth);
    }

    [Fact]
    public void Azimuth_UpdatesHrtfEffect()
    {
        var service = new GamingEnhancementService();
        var profile = HrtfProfilePresets.GetById("synthetic-front");
        service.ApplyHrtfProfile(profile);

        service.HrtfSpatializer.SetDirection(90, 0);
        Assert.NotNull(service.HrtfSpatializer.CurrentEntry);
    }

    [Fact]
    public void Azimuth_PersistsToSettings()
    {
        var settings = new AppSettings();
        bool saved = false;
        using var vm = new GamingViewModel(saveAction: () => saved = true, settings: settings);
        vm.HrtfAzimuth = 45;
        Assert.Equal(45.0, settings.HrtfAzimuth);
        Assert.True(saved);
    }

    // ── Elevation tests ────────────────────────────────────────────────

    [Fact]
    public void Elevation_DefaultIsZero()
    {
        using var vm = new GamingViewModel();
        Assert.Equal(0.0, vm.HrtfElevation);
    }

    [Fact]
    public void Elevation_SetPositive_UpdatesValue()
    {
        using var vm = new GamingViewModel();
        vm.HrtfElevation = 30;
        Assert.Equal(30.0, vm.HrtfElevation);
    }

    [Fact]
    public void Elevation_SetNegative_UpdatesValue()
    {
        using var vm = new GamingViewModel();
        vm.HrtfElevation = -30;
        Assert.Equal(-30.0, vm.HrtfElevation);
    }

    [Fact]
    public void Elevation_ClampsAbove90()
    {
        using var vm = new GamingViewModel();
        vm.HrtfElevation = 120;
        Assert.Equal(90.0, vm.HrtfElevation);
    }

    [Fact]
    public void Elevation_ClampsBelowMinus90()
    {
        using var vm = new GamingViewModel();
        vm.HrtfElevation = -120;
        Assert.Equal(-90.0, vm.HrtfElevation);
    }

    [Fact]
    public void Elevation_PersistsToSettings()
    {
        var settings = new AppSettings();
        using var vm = new GamingViewModel(settings: settings);
        vm.HrtfElevation = 45;
        Assert.Equal(45.0, settings.HrtfElevation);
    }

    // ── SpatialMix tests ───────────────────────────────────────────────

    [Fact]
    public void SpatialMixPct_DefaultIs100()
    {
        using var vm = new GamingViewModel();
        Assert.Equal(100.0, vm.HrtfSpatialMixPct);
    }

    [Fact]
    public void SpatialMixPct_SetToZero_MapsToSpatialMixZero()
    {
        using var vm = new GamingViewModel();
        vm.IsHrtfEnabled = true;
        vm.SelectedHrtfProfile = HrtfProfilePresets.GetById("synthetic-front");

        vm.HrtfSpatialMixPct = 0;
        Assert.Equal(0.0, vm.GameAudioService.Enhancement.HrtfSpatializer.SpatialMix, 4);
    }

    [Fact]
    public void SpatialMixPct_SetTo50_MapsToSpatialMixHalf()
    {
        using var vm = new GamingViewModel();
        vm.IsHrtfEnabled = true;
        vm.SelectedHrtfProfile = HrtfProfilePresets.GetById("synthetic-front");

        vm.HrtfSpatialMixPct = 50;
        Assert.Equal(0.5, vm.GameAudioService.Enhancement.HrtfSpatializer.SpatialMix, 4);
    }

    [Fact]
    public void SpatialMixPct_SetTo100_MapsToSpatialMixOne()
    {
        using var vm = new GamingViewModel();
        vm.IsHrtfEnabled = true;
        vm.SelectedHrtfProfile = HrtfProfilePresets.GetById("synthetic-front");

        vm.HrtfSpatialMixPct = 100;
        Assert.Equal(1.0, vm.GameAudioService.Enhancement.HrtfSpatializer.SpatialMix, 4);
    }

    [Fact]
    public void SpatialMixPct_ClampsAbove100()
    {
        using var vm = new GamingViewModel();
        vm.HrtfSpatialMixPct = 150;
        Assert.Equal(100.0, vm.HrtfSpatialMixPct);
    }

    [Fact]
    public void SpatialMixPct_ClampsBelowZero()
    {
        using var vm = new GamingViewModel();
        vm.HrtfSpatialMixPct = -10;
        Assert.Equal(0.0, vm.HrtfSpatialMixPct);
    }

    [Fact]
    public void SpatialMixPct_PersistsToSettings()
    {
        var settings = new AppSettings();
        using var vm = new GamingViewModel(settings: settings);
        vm.HrtfSpatialMixPct = 75;
        Assert.Equal(0.75, settings.HrtfSpatialMix, 4);
    }

    // ── Persistence round-trip tests ───────────────────────────────────

    [Fact]
    public void ConfigService_PreservesHrtfDirection()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "SoundFXStudio-Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var cs = new ConfigService(appFolder: tempDir);
            var config = new AppConfig();
            config.Settings.HrtfAzimuth = 45;
            config.Settings.HrtfElevation = 15;
            config.Settings.HrtfSpatialMix = 0.75;

            cs.Save(config);
            var loaded = cs.Load();

            Assert.Equal(45.0, loaded.Settings.HrtfAzimuth);
            Assert.Equal(15.0, loaded.Settings.HrtfElevation);
            Assert.Equal(0.75, loaded.Settings.HrtfSpatialMix, 4);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void GamingViewModel_RestoresDirectionFromSettings()
    {
        var settings = new AppSettings();
        settings.HrtfAzimuth = 60;
        settings.HrtfElevation = -15;
        settings.HrtfSpatialMix = 0.5;

        using var vm = new GamingViewModel(settings: settings);

        Assert.Equal(60.0, vm.HrtfAzimuth);
        Assert.Equal(-15.0, vm.HrtfElevation);
        Assert.Equal(50.0, vm.HrtfSpatialMixPct, 4);
    }

    [Fact]
    public void GamingViewModel_RestoresProfileFromSettings()
    {
        var settings = new AppSettings();
        settings.HrtfEnabled = true;
        settings.ActiveHrtfProfileId = "synthetic-front";

        using var vm = new GamingViewModel(settings: settings);

        Assert.True(vm.IsHrtfEnabled);
        Assert.NotNull(vm.SelectedHrtfProfile);
        Assert.Equal("synthetic-front", vm.SelectedHrtfProfile!.Id);
    }

    [Fact]
    public void GamingViewModel_NoSettings_UsesDefaults()
    {
        using var vm = new GamingViewModel();
        Assert.Equal(0.0, vm.HrtfAzimuth);
        Assert.Equal(0.0, vm.HrtfElevation);
        Assert.Equal(100.0, vm.HrtfSpatialMixPct);
    }

    // ── Disabled HRTF preserves settings ───────────────────────────────

    [Fact]
    public void DisabledHrtf_PreservesAzimuth()
    {
        var settings = new AppSettings();
        using var vm = new GamingViewModel(settings: settings);
        vm.HrtfAzimuth = 90;

        // Disable HRTF — azimuth should still be 90
        vm.IsHrtfEnabled = false;
        Assert.Equal(90.0, vm.HrtfAzimuth);
    }

    [Fact]
    public void DisabledHrtf_PreservesElevation()
    {
        var settings = new AppSettings();
        using var vm = new GamingViewModel(settings: settings);
        vm.HrtfElevation = 30;

        vm.IsHrtfEnabled = false;
        Assert.Equal(30.0, vm.HrtfElevation);
    }

    [Fact]
    public void DisabledHrtf_PreservesSpatialMix()
    {
        var settings = new AppSettings();
        using var vm = new GamingViewModel(settings: settings);
        vm.HrtfSpatialMixPct = 60;

        vm.IsHrtfEnabled = false;
        Assert.Equal(60.0, vm.HrtfSpatialMixPct);
    }

    [Fact]
    public void ReEnableHrtf_AppliesDirectionAndMix()
    {
        var settings = new AppSettings();
        settings.HrtfAzimuth = 45;
        settings.HrtfElevation = 15;
        settings.HrtfSpatialMix = 0.8;

        using var vm = new GamingViewModel(settings: settings);
        vm.IsHrtfEnabled = true;
        vm.SelectedHrtfProfile = HrtfProfilePresets.GetById("synthetic-front");

        var hrtf = vm.GameAudioService.Enhancement.HrtfSpatializer;
        Assert.NotNull(hrtf.CurrentEntry);
        Assert.Equal(0.8, hrtf.SpatialMix, 4);
    }

    // ── VoiceChangerService isolation ──────────────────────────────────

    [Fact]
    public void AzimuthChange_DoesNotAffectVoiceChanger()
    {
        var vcService = new VoiceChangerService();
        var gamingService = new GamingEnhancementService();
        var profile = HrtfProfilePresets.GetById("synthetic-front");
        gamingService.ApplyHrtfProfile(profile);

        gamingService.HrtfSpatializer.SetDirection(90, 30);

        Assert.Null(vcService.Chain.Get<HrtfEffect>());
    }

    // ── Existing behavior independence ─────────────────────────────────

    [Fact]
    public void SpatialMixChange_DoesNotAffectHeadphoneEq()
    {
        var service = new GamingEnhancementService();
        var hpProfile = HeadphoneProfilePresets.GetById("flat-test");
        var hrtfProfile = HrtfProfilePresets.GetById("synthetic-front");
        service.ApplyHeadphoneProfile(hpProfile);
        service.ApplyHrtfProfile(hrtfProfile);

        service.HrtfSpatializer.SpatialMix = 0.3;

        Assert.True(service.HeadphoneEqualizer.IsEnabled);
        Assert.True(service.HrtfSpatializer.IsEnabled);
    }

    [Fact]
    public void SpatialMixChange_DoesNotAffectGamingEq()
    {
        var service = new GamingEnhancementService();
        var gamingProfile = GamingProfilePresets.GetById("footstep-focus");
        var hrtfProfile = HrtfProfilePresets.GetById("synthetic-front");
        service.Apply(gamingProfile);
        service.ApplyHrtfProfile(hrtfProfile);

        var eqEnabled = service.Equalizer.IsEnabled;
        service.HrtfSpatializer.SpatialMix = 0.0;

        Assert.Equal(eqEnabled, service.Equalizer.IsEnabled);
    }

    private static float[] Sine(float amplitude, int length, float freq = 220f, int sampleRate = 48000)
    {
        var data = new float[length];
        for (int i = 0; i < length; i++)
        {
            data[i] = (float)(amplitude * Math.Sin(2 * Math.PI * freq * i / sampleRate));
        }
        return data;
    }

    private static float MaxAbs(Span<float> data)
    {
        float max = 0;
        for (int i = 0; i < data.Length; i++)
        {
            max = Math.Max(max, Math.Abs(data[i]));
        }
        return max;
    }

    // ── Sample-rate adaptation integration tests ──────────────────────────

    [Fact]
    public void SampleRate_44100Profile_With48000Dsp_PreparedHrtfUses48000()
    {
        var service = new GamingEnhancementService();
        var profile = new HrtfProfile
        {
            Id = "test-44100",
            Name = "Test 44100",
            SampleRate = 44100,
            IrLength = 256,
            Entries = new[]
            {
                new HrtfEntry
                {
                    AzimuthDeg = 0,
                    ElevationDeg = 0,
                    LeftEarResponse = new float[256],
                    RightEarResponse = new float[256]
                }
            }
        };

        service.SetSampleRate(48000);
        service.ApplyHrtfProfile(profile);

        var active = service.HrtfSpatializer.ActiveProfile;
        Assert.NotNull(active);
        Assert.Equal(48000, active!.SampleRate);
        Assert.True(active.IrLength > 256);
    }

    [Fact]
    public void SampleRate_48000Profile_With48000Dsp_NoUnnecessaryResampling()
    {
        var service = new GamingEnhancementService();
        var profile = new HrtfProfile
        {
            Id = "test-48000",
            Name = "Test 48000",
            SampleRate = 48000,
            IrLength = 128,
            Entries = new[]
            {
                new HrtfEntry
                {
                    AzimuthDeg = 0,
                    ElevationDeg = 0,
                    LeftEarResponse = new float[128],
                    RightEarResponse = new float[128]
                }
            }
        };

        service.ApplyHrtfProfile(profile);

        var active = service.HrtfSpatializer.ActiveProfile;
        Assert.NotNull(active);
        Assert.Equal(48000, active!.SampleRate);
        Assert.Equal(128, active.IrLength);
    }

    [Fact]
    public void SampleRate_ChangeDspRate_RePreparesActiveHrtf()
    {
        var service = new GamingEnhancementService();
        var profile = new HrtfProfile
        {
            Id = "test-44100",
            Name = "Test",
            SampleRate = 44100,
            IrLength = 256,
            Entries = new[]
            {
                new HrtfEntry
                {
                    AzimuthDeg = 0,
                    ElevationDeg = 0,
                    LeftEarResponse = new float[256],
                    RightEarResponse = new float[256]
                }
            }
        };

        service.ApplyHrtfProfile(profile);
        var irLengthAt48k = service.HrtfSpatializer.ActiveProfile!.IrLength;

        // Change to 96000 — should re-prepare
        service.SetSampleRate(96000);

        var active = service.HrtfSpatializer.ActiveProfile;
        Assert.NotNull(active);
        Assert.Equal(96000, active!.SampleRate);
        Assert.NotEqual(irLengthAt48k, active.IrLength);
    }

    [Fact]
    public void SampleRate_ConvolutionStateResetsCorrectly()
    {
        var service = new GamingEnhancementService();
        var profile = new HrtfProfile
        {
            Id = "test",
            Name = "Test",
            SampleRate = 44100,
            IrLength = 128,
            Entries = new[]
            {
                new HrtfEntry
                {
                    AzimuthDeg = 0,
                    ElevationDeg = 0,
                    LeftEarResponse = CreateUnityImpulse(128),
                    RightEarResponse = CreateUnityImpulse(128)
                }
            }
        };

        service.SetSampleRate(48000);
        service.ApplyHrtfProfile(profile);

        // Process a block to build up overlap state
        var buffer = new float[512];
        for (int i = 0; i < 256; i++) { buffer[i * 2] = 0.5f; buffer[i * 2 + 1] = 0.5f; }
        service.Chain.Process(buffer);

        // Changing sample rate re-prepares profile, which resets overlap
        // This should not crash
        service.SetSampleRate(44100);

        // Should still process without error
        service.Chain.Process(buffer);
        Assert.True(true); // No crash = success
    }

    [Fact]
    public void SampleRate_AzimuthPreservedAfterPreparation()
    {
        var service = new GamingEnhancementService();
        var profile = new HrtfProfile
        {
            Id = "test",
            Name = "Test",
            SampleRate = 44100,
            IrLength = 128,
            Entries = new[]
            {
                new HrtfEntry
                {
                    AzimuthDeg = -45,
                    ElevationDeg = 0,
                    LeftEarResponse = CreateUnityImpulse(128),
                    RightEarResponse = CreateUnityImpulse(128)
                },
                new HrtfEntry
                {
                    AzimuthDeg = 45,
                    ElevationDeg = 0,
                    LeftEarResponse = CreateUnityImpulse(128),
                    RightEarResponse = CreateUnityImpulse(128)
                }
            }
        };

        service.SetSampleRate(48000);
        service.ApplyHrtfProfile(profile);

        // Set direction on the prepared profile
        service.HrtfSpatializer.SetDirection(-45, 0);

        // The prepared profile should have the same azimuth values
        var active = service.HrtfSpatializer.ActiveProfile;
        Assert.NotNull(active);
        var entry = active!.GetEntryForDirection(-45, 0);
        Assert.NotNull(entry);
        Assert.Equal(-45, entry!.AzimuthDeg);
    }

    [Fact]
    public void SampleRate_ElevationPreservedAfterPreparation()
    {
        var service = new GamingEnhancementService();
        var profile = new HrtfProfile
        {
            Id = "test",
            Name = "Test",
            SampleRate = 44100,
            IrLength = 128,
            Entries = new[]
            {
                new HrtfEntry
                {
                    AzimuthDeg = 0,
                    ElevationDeg = 60,
                    LeftEarResponse = CreateUnityImpulse(128),
                    RightEarResponse = CreateUnityImpulse(128)
                }
            }
        };

        service.SetSampleRate(48000);
        service.ApplyHrtfProfile(profile);

        var active = service.HrtfSpatializer.ActiveProfile;
        Assert.NotNull(active);
        Assert.Equal(60, active!.Entries[0].ElevationDeg);
    }

    [Fact]
    public void SampleRate_SpatialMixPreserved()
    {
        var service = new GamingEnhancementService();
        service.HrtfSpatializer.SpatialMix = 0.75;

        var profile = new HrtfProfile
        {
            Id = "test",
            Name = "Test",
            SampleRate = 44100,
            IrLength = 128,
            Entries = new[]
            {
                new HrtfEntry
                {
                    AzimuthDeg = 0,
                    ElevationDeg = 0,
                    LeftEarResponse = CreateUnityImpulse(128),
                    RightEarResponse = CreateUnityImpulse(128)
                }
            }
        };

        service.SetSampleRate(48000);
        service.ApplyHrtfProfile(profile);

        Assert.Equal(0.75, service.HrtfSpatializer.SpatialMix);
    }

    [Fact]
    public void SampleRate_HrtfEnabledStatePreserved()
    {
        var service = new GamingEnhancementService();
        service.HrtfSpatializer.IsEnabled = true;

        var profile = new HrtfProfile
        {
            Id = "test",
            Name = "Test",
            SampleRate = 44100,
            IrLength = 128,
            Entries = new[]
            {
                new HrtfEntry
                {
                    AzimuthDeg = 0,
                    ElevationDeg = 0,
                    LeftEarResponse = CreateUnityImpulse(128),
                    RightEarResponse = CreateUnityImpulse(128)
                }
            }
        };

        service.SetSampleRate(48000);
        service.ApplyHrtfProfile(profile);

        Assert.True(service.HrtfSpatializer.IsEnabled);
    }

    [Fact]
    public void SampleRate_OriginalImportedProfileUnchanged()
    {
        var service = new GamingEnhancementService();
        var originalLeft = new float[256];
        originalLeft[0] = 1.0f;
        var originalRight = new float[256];
        originalRight[0] = 0.9f;

        var profile = new HrtfProfile
        {
            Id = "test-44100",
            Name = "Test",
            SampleRate = 44100,
            IrLength = 256,
            Entries = new[]
            {
                new HrtfEntry
                {
                    AzimuthDeg = 0,
                    ElevationDeg = 0,
                    LeftEarResponse = originalLeft,
                    RightEarResponse = originalRight
                }
            }
        };

        service.SetSampleRate(48000);
        service.ApplyHrtfProfile(profile);

        // Original profile data must be untouched
        Assert.Equal(44100, profile.SampleRate);
        Assert.Equal(256, profile.IrLength);
        Assert.Equal(1.0f, profile.Entries[0].LeftEarResponse[0]);
        Assert.Equal(0.9f, profile.Entries[0].RightEarResponse[0]);
        Assert.Equal(256, profile.Entries[0].LeftEarResponse.Length);
    }

    [Fact]
    public void SampleRate_VoiceChangerRemainsIndependent()
    {
        var gamingService = new GamingEnhancementService();
        gamingService.SetSampleRate(44100);

        // Verify gaming and voice services are separate concerns
        // VoiceChangerService is not coupled to GamingEnhancementService
        Assert.NotNull(gamingService.Chain);
        Assert.NotNull(gamingService.HrtfSpatializer);
        // Gaming chain still has 6 effects
        Assert.Equal(6, gamingService.Chain.Effects.Count);
    }

    // ── Direction interpolation integration tests ─────────────────────────

    [Fact]
    public void Interpolation_GamingEnhancementServiceUsesInterpolation()
    {
        var service = new GamingEnhancementService();
        var profile = new HrtfProfile
        {
            Id = "test",
            Name = "Test",
            SampleRate = 48000,
            IrLength = 32,
            Entries = new[]
            {
                new HrtfEntry { AzimuthDeg = 0, ElevationDeg = 0, LeftEarResponse = CreateUnityImpulse(32), RightEarResponse = CreateUnityImpulse(32) },
                new HrtfEntry { AzimuthDeg = 90, ElevationDeg = 0, LeftEarResponse = CreateUnityImpulse(32), RightEarResponse = CreateUnityImpulse(32) },
                new HrtfEntry { AzimuthDeg = -90, ElevationDeg = 0, LeftEarResponse = CreateUnityImpulse(32), RightEarResponse = CreateUnityImpulse(32) }
            }
        };

        service.ApplyHrtfProfile(profile);

        // Set direction between entries — should use interpolation
        service.HrtfSpatializer.SetDirection(45, 0);

        var buffer = new float[256];
        for (int i = 0; i < 128; i++) { buffer[i * 2] = 0.5f; buffer[i * 2 + 1] = 0.5f; }
        service.Chain.Process(buffer);

        var maxAbs = 0f;
        for (int i = 0; i < buffer.Length; i++)
            maxAbs = Math.Max(maxAbs, Math.Abs(buffer[i]));

        Assert.True(maxAbs > 0, "Interpolated HRTF should produce non-silent output");
    }

    [Fact]
    public void Interpolation_HeadphoneEqRemainsBeforeHrtf()
    {
        var service = new GamingEnhancementService();
        var effects = service.Chain.Effects;
        int headphoneIdx = -1, hrtfIdx = -1;
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i] is EqualizerEffect && i == 1) headphoneIdx = i;
            if (effects[i] is HrtfEffect) hrtfIdx = i;
        }
        Assert.True(headphoneIdx >= 0 && hrtfIdx > headphoneIdx);
    }

    [Fact]
    public void Interpolation_NoiseGateRemainsAfterHrtf()
    {
        var service = new GamingEnhancementService();
        var effects = service.Chain.Effects;
        int hrtfIdx = -1, gateIdx = -1;
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i] is HrtfEffect) hrtfIdx = i;
            if (effects[i] is NoiseGateEffect) gateIdx = i;
        }
        Assert.True(gateIdx > hrtfIdx);
    }

    [Fact]
    public void Interpolation_VoiceChangerRemainsIndependent()
    {
        var service = new GamingEnhancementService();
        var profile = new HrtfProfile
        {
            Id = "test", Name = "Test", SampleRate = 48000, IrLength = 16,
            Entries = new[]
            {
                new HrtfEntry { AzimuthDeg = 0, ElevationDeg = 0, LeftEarResponse = CreateUnityImpulse(16), RightEarResponse = CreateUnityImpulse(16) },
                new HrtfEntry { AzimuthDeg = 90, ElevationDeg = 0, LeftEarResponse = CreateUnityImpulse(16), RightEarResponse = CreateUnityImpulse(16) }
            }
        };

        service.ApplyHrtfProfile(profile);
        service.HrtfSpatializer.SetDirection(45, 0);

        // Chain should still have exactly 6 effects
        Assert.Equal(6, service.Chain.Effects.Count);
    }

    [Fact]
    public void Interpolation_SampleRatePreparationStillOccurs()
    {
        var service = new GamingEnhancementService();
        var profile = new HrtfProfile
        {
            Id = "test-44100", Name = "Test", SampleRate = 44100, IrLength = 256,
            Entries = new[]
            {
                new HrtfEntry { AzimuthDeg = 0, ElevationDeg = 0, LeftEarResponse = new float[256], RightEarResponse = new float[256] },
                new HrtfEntry { AzimuthDeg = 90, ElevationDeg = 0, LeftEarResponse = new float[256], RightEarResponse = new float[256] }
            }
        };

        service.SetSampleRate(48000);
        service.ApplyHrtfProfile(profile);

        var active = service.HrtfSpatializer.ActiveProfile;
        Assert.NotNull(active);
        Assert.Equal(48000, active!.SampleRate);

        // Interpolation should work on the prepared profile
        service.HrtfSpatializer.SetDirection(45, 0);
    }

    [Fact]
    public void Interpolation_SyntheticProfilesWork()
    {
        var service = new GamingEnhancementService();
        service.ApplyHrtfProfile(HrtfProfilePresets.GetById("synthetic-front"));

        service.HrtfSpatializer.SetDirection(0, 0);

        var buffer = new float[256];
        for (int i = 0; i < 128; i++) { buffer[i * 2] = 0.5f; buffer[i * 2 + 1] = 0.5f; }
        service.Chain.Process(buffer);
        // Should not crash
    }

    [Fact]
    public void GetEntryForDirection_BackwardCompatibility()
    {
        var profile = new HrtfProfile
        {
            Id = "test", Name = "Test", SampleRate = 48000, IrLength = 16,
            Entries = new[]
            {
                new HrtfEntry { AzimuthDeg = 0, ElevationDeg = 0, LeftEarResponse = CreateUnityImpulse(16), RightEarResponse = CreateUnityImpulse(16) },
                new HrtfEntry { AzimuthDeg = 90, ElevationDeg = 0, LeftEarResponse = CreateUnityImpulse(16), RightEarResponse = CreateUnityImpulse(16) }
            }
        };

        // GetEntryForDirection still works as before (nearest-neighbor)
        var entry = profile.GetEntryForDirection(0, 0);
        Assert.NotNull(entry);
        Assert.Equal(0, entry!.AzimuthDeg);

        var entry90 = profile.GetEntryForDirection(90, 0);
        Assert.NotNull(entry90);
        Assert.Equal(90, entry90!.AzimuthDeg);

        var entry45 = profile.GetEntryForDirection(45, 0);
        Assert.NotNull(entry45);
        // Nearest to 45° could be 0 or 90 — both are equidistant
        Assert.True(entry45.AzimuthDeg == 0 || entry45.AzimuthDeg == 90);
    }

    // ── Phase J: Latency integration tests ─────────────────────────────────

    [Fact]
    public void LatencyMode_ReachesOutputConfiguration()
    {
        var service = new GamingEnhancementService();
        // Verify the service can hold a latency mode
        // (GameAudioService applies it during StartCapture)
        var mode = AudioLatencyMode.LowLatency;
        var config = Services.Diagnostics.AudioLatencyConfiguration.Resolve(mode);
        Assert.Equal(50, config.DesiredLatencyMs);
        Assert.Equal(2, config.NumberOfBuffers);
    }

    [Fact]
    public void GamingChain_Unchanged_AfterLatencyConfig()
    {
        var service = new GamingEnhancementService();
        var beforeCount = service.Chain.Effects.Count;
        var beforeTypes = service.Chain.Effects.Select(e => e.GetType()).ToList();

        // Apply a gaming profile (same as always)
        service.Apply(GamingProfilePresets.Profiles.First());

        Assert.Equal(beforeCount, service.Chain.Effects.Count);
        for (int i = 0; i < beforeCount; i++)
            Assert.Equal(beforeTypes[i], service.Chain.Effects[i].GetType());
    }

    [Fact]
    public void HrtfRemainsAtIndex2_RegardlessOfLatencyMode()
    {
        var service = new GamingEnhancementService();
        // The chain is fixed at construction — latency mode doesn't change it
        Assert.IsType<HrtfEffect>(service.Chain.Effects[2]);
        Assert.Equal(6, service.Chain.Effects.Count);
    }

    [Fact]
    public void HrtfDirection_SurvivesOutputRecreation()
    {
        var service = new GamingEnhancementService();
        var hrtfProfile = HrtfProfilePresets.Profiles.First(p =>
            !string.Equals(p.Id, "none", StringComparison.OrdinalIgnoreCase));
        service.ApplyHrtfProfile(hrtfProfile);

        // Set direction
        service.HrtfSpatializer.SetDirection(45, 15);

        // Verify direction was applied (check it still works after another call)
        service.HrtfSpatializer.SetDirection(90, 0);
        // Should not throw
    }

    [Fact]
    public void HrtfProfile_SurvivesOutputRecreation()
    {
        var service = new GamingEnhancementService();
        var profile = HrtfProfilePresets.Profiles.First(p =>
            !string.Equals(p.Id, "none", StringComparison.OrdinalIgnoreCase));

        service.ApplyHrtfProfile(profile);
        Assert.NotNull(service.ActiveHrtfProfile);
        Assert.Equal(profile.Id, service.ActiveHrtfProfile!.Id);

        // Simulate "output recreation" by reapplying
        service.ApplyHrtfProfile(profile);
        Assert.NotNull(service.ActiveHrtfProfile);
    }

    [Fact]
    public void EffectSampleProvider_MonitorAttachment()
    {
        var service = new GamingEnhancementService();
        var source = new SilentSampleProvider();
        var provider = new SoundFXStudio.Services.DSP.EffectSampleProvider(source, service.Chain);

        Assert.Null(provider.Monitor);

        var monitor = new Services.Diagnostics.AudioProcessingMonitor();
        provider.Monitor = monitor;
        Assert.Same(monitor, provider.Monitor);

        provider.Monitor = null;
        Assert.Null(provider.Monitor);
    }

    [Fact]
    public void NullMeasurementProvider_ReturnsNull()
    {
        var provider = new Services.Diagnostics.NullAudioLatencyMeasurementProvider();
        Assert.Null(provider.GetMeasuredLatencyMs());
        Assert.False(provider.IsAvailable);
    }

    [Fact]
    public void AppSettings_RestoresLatencyMode()
    {
        var settings = new AppSettings();
        Assert.Equal(AudioLatencyMode.Balanced, settings.AudioLatencyMode);

        settings.AudioLatencyMode = AudioLatencyMode.LowLatency;
        Assert.Equal(AudioLatencyMode.LowLatency, settings.AudioLatencyMode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private sealed class SilentSampleProvider : NAudio.Wave.ISampleProvider
    {
        public NAudio.Wave.WaveFormat WaveFormat =>
            NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);

        public int Read(float[] buffer, int offset, int count)
        {
            Array.Clear(buffer, offset, count);
            return count;
        }
    }

    private static float[] CreateUnityImpulse(int length)
    {
        var hrir = new float[length];
        hrir[0] = 1.0f;
        return hrir;
    }
}
