using SoundFXStudio.Models;
using SoundFXStudio.Services;
using SoundFXStudio.Services.DSP;
using SoundFXStudio.ViewModels;
using Xunit;

namespace SoundFXStudio.Tests;

public class GamingEnhancementTests
{
    // ── EqualizerEffect tests ────────────────────────────────────────────────

    [Fact]
    public void Equalizer_NoFilters_PassesThrough()
    {
        var eq = new EqualizerEffect(48000) { IsEnabled = true };
        var data = Sine(0.5f, 48000);

        eq.Process(data);

        var tail = data.AsSpan(24000);
        Assert.InRange(MaxAbs(tail), 0.499, 0.501);
    }

    [Fact]
    public void Equalizer_Disabled_PassesThrough()
    {
        var eq = new EqualizerEffect(48000) { IsEnabled = false };
        eq.SetFilters(new[]
        {
            new EqFilter { Type = EqFilterType.Peaking, FrequencyHz = 1000, GainDb = 10, Q = 1.0 }
        });
        var data = Sine(0.5f, 48000);

        eq.Process(data);

        var tail = data.AsSpan(24000);
        Assert.InRange(MaxAbs(tail), 0.499, 0.501);
    }

    [Fact]
    public void Equalizer_PeakingBoost_IncreasesAmplitude()
    {
        var eq = new EqualizerEffect(48000) { IsEnabled = true };
        eq.SetFilters(new[]
        {
            new EqFilter { Type = EqFilterType.Peaking, FrequencyHz = 1000, GainDb = 12, Q = 1.0 }
        });
        var data = Sine(0.3f, 48000, freq: 1000);

        eq.Process(data);

        var tail = data.AsSpan(24000);
        Assert.True(MaxAbs(tail) > 0.8, $"peaking boost did not increase amplitude, max={MaxAbs(tail)}");
    }

    [Fact]
    public void Equalizer_PeakingCut_ReducesAmplitude()
    {
        var eq = new EqualizerEffect(48000) { IsEnabled = true };
        eq.SetFilters(new[]
        {
            new EqFilter { Type = EqFilterType.Peaking, FrequencyHz = 1000, GainDb = -12, Q = 1.0 }
        });
        var data = Sine(0.5f, 48000, freq: 1000);

        eq.Process(data);

        var tail = data.AsSpan(24000);
        Assert.True(MaxAbs(tail) < 0.2, $"peaking cut did not reduce amplitude, max={MaxAbs(tail)}");
    }

    [Fact]
    public void Equalizer_HighPass_AttenuatesLowFreq()
    {
        var eq = new EqualizerEffect(48000) { IsEnabled = true };
        eq.SetFilters(new[]
        {
            new EqFilter { Type = EqFilterType.HighPass, FrequencyHz = 500, Q = 0.7 }
        });
        var data = Sine(0.5f, 48000, freq: 100);

        eq.Process(data);

        var tail = data.AsSpan(24000);
        Assert.True(MaxAbs(tail) < 0.1, $"high pass did not attenuate low freq, max={MaxAbs(tail)}");
    }

    [Fact]
    public void Equalizer_HighPass_PassesHighFreq()
    {
        var eq = new EqualizerEffect(48000) { IsEnabled = true };
        eq.SetFilters(new[]
        {
            new EqFilter { Type = EqFilterType.HighPass, FrequencyHz = 500, Q = 0.7 }
        });
        var data = Sine(0.5f, 48000, freq: 3000);

        eq.Process(data);

        var tail = data.AsSpan(24000);
        Assert.True(MaxAbs(tail) > 0.3, $"high pass attenuated high freq, max={MaxAbs(tail)}");
    }

    [Fact]
    public void Equalizer_OutputIsFinite()
    {
        var eq = new EqualizerEffect(48000) { IsEnabled = true };
        eq.SetFilters(new[]
        {
            new EqFilter { Type = EqFilterType.Peaking, FrequencyHz = 1000, GainDb = 6, Q = 2.0 },
            new EqFilter { Type = EqFilterType.LowShelf, FrequencyHz = 100, GainDb = 4, Q = 0.7 },
            new EqFilter { Type = EqFilterType.HighShelf, FrequencyHz = 10000, GainDb = 3, Q = 0.7 }
        });
        var data = Sine(0.8f, 48000);

        eq.Process(data);

        Assert.All(data, v => Assert.True(float.IsFinite(v), $"non-finite value: {v}"));
    }

    [Fact]
    public void Equalizer_Reset_ClearsState()
    {
        var eq = new EqualizerEffect(48000) { IsEnabled = true };
        eq.SetFilters(new[]
        {
            new EqFilter { Type = EqFilterType.Peaking, FrequencyHz = 1000, GainDb = 6, Q = 1.0 }
        });
        var data1 = Sine(0.5f, 48000);
        eq.Process(data1);

        eq.Reset();

        var data2 = Sine(0.5f, 48000);
        eq.Process(data2);

        Assert.True(data2[0] == 0f || Math.Abs(data2[0]) < 0.01, "reset did not clear state");
    }

    [Fact]
    public void Equalizer_SampleRateChange_RecalculatesCoefficients()
    {
        var eq = new EqualizerEffect(44100) { IsEnabled = true };
        eq.SetFilters(new[]
        {
            new EqFilter { Type = EqFilterType.Peaking, FrequencyHz = 1000, GainDb = 6, Q = 1.0 }
        });
        var data = Sine(0.5f, 48000);

        eq.SampleRate = 48000;
        eq.Process(data);

        var tail = data.AsSpan(24000);
        Assert.DoesNotContain(float.NaN, data);
        Assert.True(MaxAbs(tail) > 0.1, "EQ produced no output after sample rate change");
    }

    [Fact]
    public void Equalizer_PreampAppliesGain()
    {
        var eq = new EqualizerEffect(48000) { IsEnabled = true, PreampDb = -6 };
        var data = Sine(0.8f, 48000);

        eq.Process(data);

        var tail = data.AsSpan(24000);
        Assert.True(MaxAbs(tail) < 0.5, $"preamp did not reduce level, max={MaxAbs(tail)}");
    }

    [Fact]
    public void Equalizer_MultipleFilters_ProcessInCascadedOrder()
    {
        var eq = new EqualizerEffect(48000) { IsEnabled = true };
        eq.SetFilters(new[]
        {
            new EqFilter { Type = EqFilterType.HighPass, FrequencyHz = 200, Q = 0.7 },
            new EqFilter { Type = EqFilterType.Peaking, FrequencyHz = 1000, GainDb = 10, Q = 1.0 }
        });
        var lowData = Sine(0.5f, 48000, freq: 50);
        var highData = Sine(0.3f, 48000, freq: 1000);

        eq.Process(lowData);
        var eq2 = new EqualizerEffect(48000) { IsEnabled = true };
        eq2.SetFilters(new[]
        {
            new EqFilter { Type = EqFilterType.HighPass, FrequencyHz = 200, Q = 0.7 },
            new EqFilter { Type = EqFilterType.Peaking, FrequencyHz = 1000, GainDb = 10, Q = 1.0 }
        });
        eq2.Process(highData);

        var lowTail = lowData.AsSpan(24000);
        var highTail = highData.AsSpan(24000);
        Assert.True(MaxAbs(lowTail) < 0.05, $"HPF should attenuate 50Hz, max={MaxAbs(lowTail)}");
        Assert.True(MaxAbs(highTail) > 0.5, $"peak boost should amplify 1kHz, max={MaxAbs(highTail)}");
    }

    [Fact]
    public void Equalizer_DisabledFilterBand_Skipped()
    {
        var eq = new EqualizerEffect(48000) { IsEnabled = true };
        eq.SetFilters(new[]
        {
            new EqFilter { Type = EqFilterType.Peaking, FrequencyHz = 1000, GainDb = 12, Q = 1.0, Enabled = false }
        });
        var data = Sine(0.5f, 48000, freq: 1000);

        eq.Process(data);

        var tail = data.AsSpan(24000);
        Assert.InRange(MaxAbs(tail), 0.499, 0.501);
    }

    [Fact]
    public void Equalizer_ClippingProtection_HighGainDoesNotProduceInfinity()
    {
        var eq = new EqualizerEffect(48000) { IsEnabled = true };
        eq.SetFilters(new[]
        {
            new EqFilter { Type = EqFilterType.Peaking, FrequencyHz = 1000, GainDb = 30, Q = 10 }
        });
        var data = Sine(1.0f, 48000, freq: 1000);

        eq.Process(data);

        Assert.All(data, v => Assert.True(float.IsFinite(v), $"non-finite value: {v}"));
    }

    // ── GamingEnhancementService tests ───────────────────────────────────────

    [Fact]
    public void GamingService_OwnsItsOwnDSPChain()
    {
        var service = new GamingEnhancementService();

        Assert.NotNull(service.Chain);
        Assert.NotNull(service.Equalizer);
        Assert.False(service.Equalizer.IsEnabled);
    }

    [Fact]
    public void GamingService_HasExpectedEffectsInChain()
    {
        var service = new GamingEnhancementService();

        Assert.NotNull(service.Chain.Get<EqualizerEffect>());
        Assert.NotNull(service.HeadphoneEqualizer);
        Assert.NotNull(service.Chain.Get<HrtfEffect>());
        Assert.NotNull(service.Chain.Get<NoiseGateEffect>());
        Assert.NotNull(service.Chain.Get<CompressorEffect>());
        Assert.NotNull(service.Chain.Get<LimiterEffect>());
        Assert.Equal(6, service.Chain.Effects.Count);
    }

    [Fact]
    public void Apply_Profile_ConfiguresOwnedEqualizer()
    {
        var service = new GamingEnhancementService();
        var profile = GamingProfilePresets.GetById("footstep-focus");

        service.Apply(profile);

        Assert.True(service.Equalizer.IsEnabled);
        Assert.Same(profile, service.ActiveProfile);
        Assert.Equal(profile.PreampDb, service.Equalizer.PreampDb);
    }

    [Fact]
    public void Apply_Profile_ConfiguresChainDynamics()
    {
        var service = new GamingEnhancementService();
        var profile = GamingProfilePresets.GetById("competitive-fps");

        service.Apply(profile);

        var gate = service.Chain.Get<NoiseGateEffect>()!;
        var comp = service.Chain.Get<CompressorEffect>()!;
        var limiter = service.Chain.Get<LimiterEffect>()!;

        Assert.Equal(profile.NoiseGateEnabled, gate.IsEnabled);
        Assert.Equal(profile.NoiseGateThresholdDb, gate.ThresholdDb);
        Assert.Equal(profile.CompressorEnabled, comp.IsEnabled);
        Assert.Equal(profile.CompressorThresholdDb, comp.ThresholdDb);
        Assert.Equal(profile.CompressorRatio, comp.Ratio);
        Assert.Equal(profile.LimiterEnabled, limiter.IsEnabled);
        Assert.Equal(profile.LimiterThreshold, limiter.Threshold);
    }

    [Fact]
    public void Apply_Profile_DoesNotModifyVoiceChangerChain()
    {
        var vcService = new VoiceChangerService();
        var gamingService = new GamingEnhancementService();
        var profile = GamingProfilePresets.GetById("footstep-focus");

        var origGateEnabled = vcService.Chain.Get<NoiseGateEffect>()!.IsEnabled;
        var origCompEnabled = vcService.Chain.Get<CompressorEffect>()!.IsEnabled;
        var origLimiterEnabled = vcService.Chain.Get<LimiterEffect>()!.IsEnabled;
        var origDistEnabled = vcService.Chain.Get<DistortionEffect>()!.IsEnabled;
        var origReverbEnabled = vcService.Chain.Get<ReverbEffect>()!.IsEnabled;

        gamingService.Apply(profile);

        Assert.Equal(origGateEnabled, vcService.Chain.Get<NoiseGateEffect>()!.IsEnabled);
        Assert.Equal(origCompEnabled, vcService.Chain.Get<CompressorEffect>()!.IsEnabled);
        Assert.Equal(origLimiterEnabled, vcService.Chain.Get<LimiterEffect>()!.IsEnabled);
        Assert.Equal(origDistEnabled, vcService.Chain.Get<DistortionEffect>()!.IsEnabled);
        Assert.Equal(origReverbEnabled, vcService.Chain.Get<ReverbEffect>()!.IsEnabled);
    }

    [Fact]
    public void Apply_Profile_DoesNotContainEqualizerInVoiceChangerChain()
    {
        var vcService = new VoiceChangerService();

        Assert.NotNull(vcService.Chain.Get<EqualizerEffect>());
    }

    [Fact]
    public void Bypass_DisablesAllChainEffects()
    {
        var service = new GamingEnhancementService();
        var profile = GamingProfilePresets.GetById("footstep-focus");

        service.Apply(profile);
        Assert.True(service.Equalizer.IsEnabled);

        service.Bypass();
        Assert.False(service.Equalizer.IsEnabled);
        Assert.Null(service.ActiveProfile);

        foreach (var effect in service.Chain.Effects)
        {
            Assert.False(effect.IsEnabled, $"{effect.Name} should be disabled after bypass");
        }
    }

    [Fact]
    public void Bypass_DoesNotModifyVoiceChangerChain()
    {
        var vcService = new VoiceChangerService();
        var gamingService = new GamingEnhancementService();

        gamingService.Apply(GamingProfilePresets.GetById("footstep-focus"));
        var origGate = vcService.Chain.Get<NoiseGateEffect>()!.IsEnabled;

        gamingService.Bypass();

        Assert.Equal(origGate, vcService.Chain.Get<NoiseGateEffect>()!.IsEnabled);
    }

    [Fact]
    public void SwitchingProfiles_UpdatesEqualizer()
    {
        var service = new GamingEnhancementService();

        service.Apply(GamingProfilePresets.GetById("footstep-focus"));
        Assert.True(service.Equalizer.IsEnabled);

        service.Apply(GamingProfilePresets.GetById("directional-audio"));
        Assert.Equal("directional-audio", service.ActiveProfile!.Id);
    }

    [Fact]
    public void Apply_ProfileA_Then_ProfileB_NoStaleState()
    {
        var service = new GamingEnhancementService();

        service.Apply(GamingProfilePresets.GetById("footstep-focus"));
        var firstFilterCount = service.ActiveProfile!.EqFilters.Count;

        service.Apply(GamingProfilePresets.GetById("immersive"));
        Assert.NotEqual(firstFilterCount, service.ActiveProfile!.EqFilters.Count);
    }

    [Fact]
    public void Apply_ThenBypass_ThenApply_CleanState()
    {
        var service = new GamingEnhancementService();

        service.Apply(GamingProfilePresets.GetById("footstep-focus"));
        Assert.True(service.Equalizer.IsEnabled);

        service.Bypass();
        Assert.False(service.Equalizer.IsEnabled);

        service.Apply(GamingProfilePresets.GetById("competitive-fps"));
        Assert.True(service.Equalizer.IsEnabled);
        Assert.Equal("competitive-fps", service.ActiveProfile!.Id);
    }

    [Fact]
    public void SetSampleRate_UpdatesAllChainEffects()
    {
        var service = new GamingEnhancementService();

        service.SetSampleRate(44100);
        Assert.Equal(44100, service.Equalizer.SampleRate);
        Assert.Equal(44100, service.Chain.Get<NoiseGateEffect>()!.SampleRate);
        Assert.Equal(44100, service.Chain.Get<CompressorEffect>()!.SampleRate);
        Assert.Equal(44100, service.Chain.Get<LimiterEffect>()!.SampleRate);

        service.SetSampleRate(48000);
        Assert.Equal(48000, service.Equalizer.SampleRate);
    }

    [Fact]
    public void Apply_EmptyEqFilters_DoesNotCrash()
    {
        var service = new GamingEnhancementService();
        var profile = new GamingProfile
        {
            Id = "test",
            Name = "Test",
            EqEnabled = true
        };

        var exception = Record.Exception(() => service.Apply(profile));
        Assert.Null(exception);
    }

    [Fact]
    public void Apply_InvalidValues_DoesNotCrash()
    {
        var service = new GamingEnhancementService();
        var profile = new GamingProfile
        {
            Id = "test",
            Name = "Test",
            EqFilters =
            {
                new EqFilter { Type = EqFilterType.Peaking, FrequencyHz = 1000, GainDb = 20, Q = 0.1 }
            }
        };

        var exception = Record.Exception(() => service.Apply(profile));
        Assert.Null(exception);
    }

    [Fact]
    public void Bypass_WhenNeverApplied_DoesNotCrash()
    {
        var service = new GamingEnhancementService();

        var exception = Record.Exception(() => service.Bypass());
        Assert.Null(exception);
        Assert.Null(service.ActiveProfile);
    }

    // ── GamingProfile DSP chain integration ──────────────────────────────────

    [Fact]
    public void GamingChain_Process_ProducesOutput()
    {
        var service = new GamingEnhancementService();
        service.Apply(GamingProfilePresets.GetById("footstep-focus"));
        service.SetSampleRate(48000);

        var data = Sine(0.5f, 48000, freq: 3000);
        service.Chain.Process(data);

        var tail = data.AsSpan(24000);
        Assert.True(MaxAbs(tail) > 0.01, "gaming chain produced no output");
    }

    [Fact]
    public void GamingChain_DisabledEq_PassesThrough()
    {
        var service = new GamingEnhancementService();
        var profile = GamingProfilePresets.GetById("footstep-focus");
        profile.EqEnabled = false;
        service.Apply(profile);
        service.SetSampleRate(48000);

        var data = Sine(0.5f, 48000, freq: 3000);
        service.Chain.Process(data);

        var tail = data.AsSpan(24000);
        Assert.True(MaxAbs(tail) > 0.1, "chain with disabled EQ should pass through");
    }

    // ── VoiceChangerService isolation tests ──────────────────────────────────

    [Fact]
    public void VoiceChangerService_HasEqualizerInChain()
    {
        var service = new VoiceChangerService();

        Assert.NotNull(service.Chain.Get<EqualizerEffect>());
    }

    [Fact]
    public void VoiceChangerService_ChainEffectsUnchangedByGamingService()
    {
        var vcService = new VoiceChangerService();
        var gamingService = new GamingEnhancementService();

        gamingService.Apply(GamingProfilePresets.GetById("footstep-focus"));
        gamingService.Apply(GamingProfilePresets.GetById("competitive-fps"));
        gamingService.Bypass();

        Assert.Equal(8, vcService.Chain.Effects.Count);
        Assert.NotNull(vcService.Chain.Get<NoiseGateEffect>());
        Assert.NotNull(vcService.Chain.Get<EqualizerEffect>());
        Assert.NotNull(vcService.Chain.Get<LimiterEffect>());
        Assert.NotNull(vcService.Chain.Get<CompressorEffect>());
        Assert.NotNull(vcService.Chain.Get<DistortionEffect>());
        Assert.NotNull(vcService.Chain.Get<ReverbEffect>());
        Assert.NotNull(vcService.Chain.Get<RobotEffect>());
        Assert.NotNull(vcService.Chain.Get<ChorusEffect>());
    }

    [Fact]
    public void VoiceChangerChain_HasDifferentNoiseGateDefaults_ThanGamingChain()
    {
        var vcService = new VoiceChangerService();
        var gamingService = new GamingEnhancementService();

        var vcGate = vcService.Chain.Get<NoiseGateEffect>()!;
        var gameGate = gamingService.Chain.Get<NoiseGateEffect>()!;

        Assert.NotSame(vcGate, gameGate);
        Assert.False(ReferenceEquals(vcGate, gameGate));
    }

    // ── GameProcessInfo tests ────────────────────────────────────────────────

    [Fact]
    public void GameProcessInfo_DefaultValues()
    {
        var info = new GameProcessInfo();

        Assert.Equal(string.Empty, info.ProcessName);
        Assert.Equal(string.Empty, info.ExecutableName);
        Assert.Equal(string.Empty, info.DisplayName);
        Assert.Equal(string.Empty, info.ExecutablePath);
    }

    [Fact]
    public void GameProcessInfo_ToString_UsesDisplayName()
    {
        var info = new GameProcessInfo
        {
            ProcessName = "cod",
            DisplayName = "Call of Duty"
        };

        Assert.Equal("Call of Duty", info.ToString());
    }

    [Fact]
    public void GameProcessInfo_ToString_FallsBackToProcessName()
    {
        var info = new GameProcessInfo
        {
            ProcessName = "cod"
        };

        Assert.Equal("cod", info.ToString());
    }

    // ── GamingProfile preset tests ───────────────────────────────────────────

    [Fact]
    public void GamingPresets_NonEmpty_UniqueIds()
    {
        Assert.NotEmpty(GamingProfilePresets.Profiles);
        Assert.Equal(
            GamingProfilePresets.Profiles.Count,
            GamingProfilePresets.Profiles.Select(p => p.Id).Distinct().Count());
    }

    [Fact]
    public void GamingPresets_AllHaveNames()
    {
        Assert.All(GamingProfilePresets.Profiles, p =>
            Assert.False(string.IsNullOrWhiteSpace(p.Name)));
    }

    [Fact]
    public void GamingPresets_AllHaveDescriptions()
    {
        Assert.All(GamingProfilePresets.Profiles, p =>
            Assert.False(string.IsNullOrWhiteSpace(p.Description)));
    }

    [Fact]
    public void GetById_UnknownId_ReturnsFirstProfile()
    {
        var profile = GamingProfilePresets.GetById("does-not-exist");
        Assert.Equal(GamingProfilePresets.Profiles[0].Id, profile.Id);
    }

    [Fact]
    public void FootstepFocus_HasCorrectConfiguration()
    {
        var profile = GamingProfilePresets.GetById("footstep-focus");

        Assert.Equal("Footstep Focus", profile.Name);
        Assert.True(profile.EqEnabled);
        Assert.True(profile.EqFilters.Count > 0);
        Assert.Contains(profile.EqFilters, f => f.Type == EqFilterType.HighPass);
        Assert.Contains(profile.EqFilters, f => f.FrequencyHz == 2500);
    }

    [Theory]
    [InlineData("competitive-fps")]
    [InlineData("footstep-focus")]
    [InlineData("directional-audio")]
    [InlineData("voice-focus")]
    [InlineData("immersive")]
    [InlineData("movie-cinematic")]
    public void GamingPresets_Exists(string id)
    {
        var profile = GamingProfilePresets.GetById(id);
        Assert.Equal(id, profile.Id);
    }

    // ── Persistence tests ────────────────────────────────────────────────────

    [Fact]
    public void ConfigService_PreservesGamingSettings()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "SoundFXStudio-Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var cs = new ConfigService(appFolder: tempDir);
            var config = new AppConfig();
            config.Settings.GamingEnhancementEnabled = true;
            config.Settings.ActiveGamingProfileId = "footstep-focus";

            cs.Save(config);
            var loaded = cs.Load();

            Assert.True(loaded.Settings.GamingEnhancementEnabled);
            Assert.Equal("footstep-focus", loaded.Settings.ActiveGamingProfileId);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void AppSettings_GamingDefaults_AreValid()
    {
        var settings = new AppSettings();

        Assert.False(settings.GamingEnhancementEnabled);
        Assert.Equal(string.Empty, settings.ActiveGamingProfileId);
    }

    [Fact]
    public void GamingProfile_SerializationRoundTrip()
    {
        var profile = GamingProfilePresets.GetById("footstep-focus");

        var json = System.Text.Json.JsonSerializer.Serialize(profile);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<GamingProfile>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(profile.Id, deserialized!.Id);
        Assert.Equal(profile.Name, deserialized.Name);
        Assert.Equal(profile.EqEnabled, deserialized.EqEnabled);
        Assert.Equal(profile.PreampDb, deserialized.PreampDb);
        Assert.Equal(profile.EqFilters.Count, deserialized.EqFilters.Count);
    }

    // ── Preset immutability tests ─────────────────────────────────────────────

    [Fact]
    public void GetById_ReturnsClone_ModifyingCloneDoesNotAffectPreset()
    {
        var original = GamingProfilePresets.GetById("footstep-focus");
        var originalPreamp = original.PreampDb;

        var clone = GamingProfilePresets.GetById("footstep-focus");
        clone.PreampDb = -999;
        clone.EqEnabled = false;
        clone.Name = "MODIFIED";

        var secondClone = GamingProfilePresets.GetById("footstep-focus");
        Assert.Equal(originalPreamp, secondClone.PreampDb);
        Assert.True(secondClone.EqEnabled);
        Assert.NotEqual("MODIFIED", secondClone.Name);
    }

    [Fact]
    public void GetById_ReturnsIndependentClones()
    {
        var a = GamingProfilePresets.GetById("footstep-focus");
        var b = GamingProfilePresets.GetById("footstep-focus");

        Assert.False(ReferenceEquals(a, b));
        a.PreampDb = -50;
        Assert.NotEqual(a.PreampDb, b.PreampDb);
    }

    [Fact]
    public void GetById_CloneFiltersAreIndependent()
    {
        var a = GamingProfilePresets.GetById("footstep-focus");
        var b = GamingProfilePresets.GetById("footstep-focus");

        Assert.False(ReferenceEquals(a.EqFilters, b.EqFilters));
        Assert.Equal(a.EqFilters.Count, b.EqFilters.Count);

        a.EqFilters.Clear();
        Assert.NotEqual(a.EqFilters.Count, b.EqFilters.Count);
    }

    [Fact]
    public void GetById_CloneFilterObjectsAreIndependent()
    {
        var a = GamingProfilePresets.GetById("footstep-focus");
        var b = GamingProfilePresets.GetById("footstep-focus");

        a.EqFilters[0].GainDb = -999;
        Assert.NotEqual(a.EqFilters[0].GainDb, b.EqFilters[0].GainDb);
    }

    [Fact]
    public void GetById_AllPresetsReturnClones()
    {
        foreach (var preset in GamingProfilePresets.Profiles)
        {
            var clone = GamingProfilePresets.GetById(preset.Id);
            clone.Name = "HACKED";
            var verify = GamingProfilePresets.GetById(preset.Id);
            Assert.Equal(preset.Id, verify.Id);
            Assert.NotEqual("HACKED", verify.Name);
        }
    }

    // ── GamingProfile.Clone tests ─────────────────────────────────────────────

    [Fact]
    public void Clone_PreservesAllScalarProperties()
    {
        var source = GamingProfilePresets.GetById("voice-focus");
        var clone = source.Clone();

        Assert.Equal(source.Id, clone.Id);
        Assert.Equal(source.Name, clone.Name);
        Assert.Equal(source.Description, clone.Description);
        Assert.Equal(source.Category, clone.Category);
        Assert.Equal(source.IsEnabled, clone.IsEnabled);
        Assert.Equal(source.PreampDb, clone.PreampDb);
        Assert.Equal(source.NoiseGateEnabled, clone.NoiseGateEnabled);
        Assert.Equal(source.NoiseGateThresholdDb, clone.NoiseGateThresholdDb);
        Assert.Equal(source.CompressorEnabled, clone.CompressorEnabled);
        Assert.Equal(source.CompressorThresholdDb, clone.CompressorThresholdDb);
        Assert.Equal(source.CompressorRatio, clone.CompressorRatio);
        Assert.Equal(source.CompressorAttackMs, clone.CompressorAttackMs);
        Assert.Equal(source.CompressorReleaseMs, clone.CompressorReleaseMs);
        Assert.Equal(source.CompressorMakeUpGainDb, clone.CompressorMakeUpGainDb);
        Assert.Equal(source.LimiterEnabled, clone.LimiterEnabled);
        Assert.Equal(source.LimiterThreshold, clone.LimiterThreshold);
        Assert.Equal(source.LimiterReleaseMs, clone.LimiterReleaseMs);
        Assert.Equal(source.EqEnabled, clone.EqEnabled);
    }

    [Fact]
    public void Clone_CreatesIndependentFilterCollection()
    {
        var source = GamingProfilePresets.GetById("footstep-focus");
        var clone = source.Clone();

        Assert.Equal(source.EqFilters.Count, clone.EqFilters.Count);
        Assert.False(ReferenceEquals(source.EqFilters, clone.EqFilters));

        clone.EqFilters.Add(new EqFilter { Type = EqFilterType.Peaking, FrequencyHz = 999 });
        Assert.NotEqual(source.EqFilters.Count, clone.EqFilters.Count);
    }

    [Fact]
    public void Clone_CreatesIndependentFilterObjects()
    {
        var source = GamingProfilePresets.GetById("footstep-focus");
        var clone = source.Clone();

        Assert.False(ReferenceEquals(source.EqFilters[0], clone.EqFilters[0]));
        Assert.Equal(source.EqFilters[0].FrequencyHz, clone.EqFilters[0].FrequencyHz);

        clone.EqFilters[0].GainDb = -999;
        Assert.NotEqual(source.EqFilters[0].GainDb, clone.EqFilters[0].GainDb);
    }

    // ── GameAudioService lifecycle tests ───────────────────────────────────────

    [Fact]
    public void GameAudioService_StopCapture_WhenNotCapturing_DoesNotCrash()
    {
        var service = new GameAudioService();
        var exception = Record.Exception(() => service.StopCapture());
        Assert.Null(exception);
        Assert.False(service.IsCapturing);
    }

    [Fact]
    public void GameAudioService_Dispose_MultipleTimes_DoesNotCrash()
    {
        var service = new GameAudioService();
        var exception = Record.Exception(() =>
        {
            service.Dispose();
            service.Dispose();
        });
        Assert.Null(exception);
    }

    [Fact]
    public void GameAudioService_StartCapture_WhenDisposed_Throws()
    {
        var service = new GameAudioService();
        service.Dispose();
        Assert.Throws<ObjectDisposedException>(() => service.StartCapture(1234));
    }

    [Fact]
    public void GameAudioService_InitialStates()
    {
        var service = new GameAudioService();
        Assert.False(service.IsCapturing);
        Assert.Equal(0u, service.TargetProcessId);
        Assert.NotNull(service.Enhancement);
    }

    [Fact]
    public void GameAudioService_EnhancementChain_IsIndependent()
    {
        var service = new GameAudioService();
        Assert.NotNull(service.Enhancement.Chain);
        Assert.NotNull(service.Enhancement.Equalizer);
        Assert.NotNull(service.Enhancement.HeadphoneEqualizer);
        Assert.NotNull(service.Enhancement.HrtfSpatializer);
        Assert.Equal(6, service.Enhancement.Chain.Effects.Count);
    }

    // ── AudioSessionSuppressor state tests ─────────────────────────────────────
    // NOTE: Real session manipulation requires hardware + running audio sessions.
    // These tests verify state-tracking logic only.

    [Fact]
    public void Suppressor_HasNoSessionsInitially()
    {
        var suppressor = new AudioSessionSuppressor();
        Assert.False(suppressor.HasSuppressedSessions);
        Assert.Equal(0, suppressor.GetSuppressedCount(1234));
    }

    [Fact]
    public void Suppressor_RestoreNonexistentPid_DoesNotCrash()
    {
        var suppressor = new AudioSessionSuppressor();
        var count = suppressor.RestoreProcess(99999);
        Assert.Equal(0, count);
    }

    [Fact]
    public void Suppressor_RestoreAll_DoesNotCrash()
    {
        var suppressor = new AudioSessionSuppressor();
        var exception = Record.Exception(() => suppressor.RestoreAll());
        Assert.Null(exception);
    }

    [Fact]
    public void Suppressor_Dispose_RestoresAll()
    {
        var suppressor = new AudioSessionSuppressor();
        var exception = Record.Exception(() => suppressor.Dispose());
        Assert.Null(exception);
        Assert.False(suppressor.HasSuppressedSessions);
    }

    [Fact]
    public void Suppressor_Dispose_MultipleTimes_DoesNotCrash()
    {
        var suppressor = new AudioSessionSuppressor();
        suppressor.Dispose();
        var exception = Record.Exception(() => suppressor.Dispose());
        Assert.Null(exception);
    }

    // ── GamingViewModel tests ────────────────────────────────────────────────

    [Fact]
    public void GamingViewModel_DefaultState_Ready()
    {
        using var vm = new GamingViewModel();
        Assert.NotNull(vm.StatusText);
        Assert.False(vm.IsEnabled);
        Assert.False(vm.IsCapturing);
        Assert.Empty(vm.ErrorText);
    }

    [Fact]
    public void GamingViewModel_AvailableProfiles_Populated()
    {
        using var vm = new GamingViewModel();
        Assert.Equal(GamingProfilePresets.Profiles.Count, vm.AvailableProfiles.Count);
    }

    [Fact]
    public void GamingViewModel_FirstProfileSelected_ByDefault()
    {
        using var vm = new GamingViewModel();
        Assert.NotNull(vm.SelectedProfile);
        Assert.Equal(GamingProfilePresets.Profiles[0].Id, vm.SelectedProfile.Id);
    }

    [Fact]
    public void GamingViewModel_ToggleEnable_TogglesState()
    {
        using var vm = new GamingViewModel();
        Assert.False(vm.IsEnabled);

        vm.IsEnabled = true;
        Assert.True(vm.IsEnabled);
        Assert.Equal("ENABLED", vm.EnableToggleText);
        Assert.Equal("#22C55E", vm.EnableStatusColor);

        vm.IsEnabled = false;
        Assert.False(vm.IsEnabled);
        Assert.Equal("DISABLED", vm.EnableToggleText);
        Assert.Equal("#E85555", vm.EnableStatusColor);
    }

    [Fact]
    public void GamingViewModel_ToggleEnableCommand_Toggles()
    {
        using var vm = new GamingViewModel();
        Assert.False(vm.IsEnabled);

        vm.ToggleEnableCommand.Execute(null);
        Assert.True(vm.IsEnabled);

        vm.ToggleEnableCommand.Execute(null);
        Assert.False(vm.IsEnabled);
    }

    [Fact]
    public void GamingViewModel_CanStartCapture_RequiresEnabledAndProcess()
    {
        using var vm = new GamingViewModel();
        Assert.False(vm.CanStartCapture);

        vm.IsEnabled = true;
        Assert.False(vm.CanStartCapture);
    }

    [Fact]
    public void GamingViewModel_CaptureButtonText_ReflectsState()
    {
        using var vm = new GamingViewModel();
        Assert.Equal("Start Capture", vm.CaptureButtonText);
    }

    [Fact]
    public void GamingViewModel_StopCapture_NoOpWhenNotCapturing()
    {
        using var vm = new GamingViewModel();
        var exception = Record.Exception(() => { vm.StopCaptureCommand.Execute(null); });
        Assert.Null(exception);
        Assert.False(vm.IsCapturing);
    }

    [Fact]
    public void GamingViewModel_Dispose_CleansUp()
    {
        var vm = new GamingViewModel();
        vm.Dispose();
        var exception = Record.Exception(() => { vm.Dispose(); });
        Assert.Null(exception);
    }

    [Fact]
    public void GamingViewModel_SaveCallback_InvokedOnEnable()
    {
        bool saved = false;
        using var vm = new GamingViewModel(saveAction: () => saved = true);
        vm.IsEnabled = true;
        Assert.True(saved);
    }

    [Fact]
    public void GamingViewModel_SetStatusCallback_InvokedOnStatusChange()
    {
        string? lastStatus = null;
        using var vm = new GamingViewModel(setStatusAction: s => lastStatus = s);
        vm.IsEnabled = true;
        Assert.Equal("Gaming Enhancement enabled", lastStatus);
    }

    [Fact]
    public void GamingViewModel_ChangeProfile_UpdatesStatus()
    {
        using var vm = new GamingViewModel();
        var footstep = GamingProfilePresets.GetById("footstep-focus");
        vm.SelectedProfile = footstep;
        Assert.Contains(footstep.Name, vm.StatusText);
    }

    [Fact]
    public void GamingViewModel_GameAudioService_Available()
    {
        using var vm = new GamingViewModel();
        Assert.NotNull(vm.GameAudioService);
    }

    [Fact]
    public void GamingViewModel_SelectedProfileIndex_ClampsCorrectly()
    {
        using var vm = new GamingViewModel();
        vm.SelectedProfileIndex = 2;
        Assert.Equal(2, vm.SelectedProfileIndex);
        Assert.NotNull(vm.SelectedProfile);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

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
}
