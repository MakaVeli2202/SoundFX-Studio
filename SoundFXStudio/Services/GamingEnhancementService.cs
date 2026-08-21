using SoundFXStudio.Models;
using SoundFXStudio.Services.DSP;
using SoundFXStudio.Services.Hrtf;

namespace SoundFXStudio.Services;

/// <summary>
/// Manages the gaming audio enhancement pipeline.
/// Owns an independent DSPChain with EQ + spatial + dynamics processing for game audio.
///
/// Architecture:
/// Game Audio (via ProcessLoopbackCapture) → DSPChain → Voicemeeter → speakers/headphones.
/// VoiceChangerService is completely independent and untouched.
///
/// DSPChain order: GamingEqualizer → HeadphoneEqualizer → HRTF → NoiseGate → Compressor → Limiter.
/// All effects are owned by this service — zero coupling to VoiceChangerService.
/// </summary>
public sealed class GamingEnhancementService
{
    private GamingProfile? _activeProfile;
    private HeadphoneProfile? _activeHeadphoneProfile;
    private HrtfProfile? _activeHrtfProfile;
    private int _currentSampleRate = 48000;

    public GamingEnhancementService()
    {
        Chain.Add(new EqualizerEffect());
        Chain.Add(new EqualizerEffect());
        Chain.Add(new HrtfEffect(48000));
        Chain.Add(new NoiseGateEffect(48000));
        Chain.Add(new CompressorEffect(48000));
        Chain.Add(new LimiterEffect(48000));
    }

    /// <summary>
    /// The gaming DSP chain. Contains gaming equalizer, headphone equalizer,
    /// noise gate, compressor, and limiter.
    /// Game audio samples pass through this chain before reaching Voicemeeter.
    /// </summary>
    public DSPChain Chain { get; } = new();

    /// <summary>
    /// Profile preparer: handles HRIR resampling when profile sample rate != DSP sample rate.
    /// </summary>
    public HrtfProfilePreparer ProfilePreparer { get; } = new();

    /// <summary>
    /// Convenience accessor for the gaming equalizer effect in the chain.
    /// </summary>
    public EqualizerEffect Equalizer => Chain.Get<EqualizerEffect>()!;

    /// <summary>
    /// Convenience accessor for the headphone equalizer effect in the chain.
    /// This is the second EqualizerEffect instance, used for headphone compensation.
    /// </summary>
    public EqualizerEffect HeadphoneEqualizer => Chain.Effects
        .OfType<EqualizerEffect>()
        .Skip(1)
        .FirstOrDefault()!;

    /// <summary>
    /// Convenience accessor for the HRTF spatializer effect in the chain.
    /// This is the third effect, positioned after headphone EQ and before noise gate.
    /// </summary>
    public HrtfEffect HrtfSpatializer => Chain.Get<HrtfEffect>()!;

    /// <summary>
    /// The currently active gaming profile, or null if none is active.
    /// </summary>
    public GamingProfile? ActiveProfile => _activeProfile;

    /// <summary>
    /// The currently active headphone profile, or null if none is active.
    /// </summary>
    public HeadphoneProfile? ActiveHeadphoneProfile => _activeHeadphoneProfile;

    /// <summary>
    /// The currently active HRTF profile, or null if none is active.
    /// </summary>
    public HrtfProfile? ActiveHrtfProfile => _activeHrtfProfile;

    /// <summary>
    /// Applies a gaming profile by configuring all effects in the owned DSP chain.
    /// Does NOT modify any voice-changer effects or the headphone equalizer.
    /// </summary>
    public void Apply(GamingProfile profile)
    {
        _activeProfile = profile;

        var eq = Equalizer;
        if (eq is not null)
        {
            eq.IsEnabled = profile.EqEnabled;
            eq.PreampDb = profile.PreampDb;
            eq.SetFilters(profile.EqFilters);
        }

        var gate = Chain.Get<NoiseGateEffect>();
        if (gate is not null)
        {
            gate.IsEnabled = profile.NoiseGateEnabled;
            gate.ThresholdDb = profile.NoiseGateThresholdDb;
        }

        var comp = Chain.Get<CompressorEffect>();
        if (comp is not null)
        {
            comp.IsEnabled = profile.CompressorEnabled;
            comp.ThresholdDb = profile.CompressorThresholdDb;
            comp.Ratio = profile.CompressorRatio;
            comp.AttackMs = profile.CompressorAttackMs;
            comp.ReleaseMs = profile.CompressorReleaseMs;
            comp.MakeUpGainDb = profile.CompressorMakeUpGainDb;
        }

        var limiter = Chain.Get<LimiterEffect>();
        if (limiter is not null)
        {
            limiter.IsEnabled = profile.LimiterEnabled;
            limiter.Threshold = profile.LimiterThreshold;
            limiter.ReleaseMs = profile.LimiterReleaseMs;
        }
    }

    /// <summary>
    /// Applies a headphone EQ profile by configuring the headphone equalizer effect.
    /// This is independent of the gaming profile and only affects the headphone compensation EQ.
    /// Pass null to disable headphone EQ.
    /// </summary>
    public void ApplyHeadphoneProfile(HeadphoneProfile? profile)
    {
        _activeHeadphoneProfile = profile;
        var hpEq = HeadphoneEqualizer;
        if (hpEq is null) return;

        if (profile is null)
        {
            hpEq.IsEnabled = false;
            hpEq.SetFilters(Array.Empty<EqFilter>());
            hpEq.PreampDb = 0;
            return;
        }

        hpEq.IsEnabled = true;
        hpEq.PreampDb = profile.PreampDb;
        hpEq.SetFilters(profile.Filters);
    }

    /// <summary>
    /// Applies an HRTF profile by preparing it for the current DSP sample rate
    /// and configuring the HRTF spatializer effect.
    /// Pass null to disable HRTF spatialization.
    /// </summary>
    public void ApplyHrtfProfile(HrtfProfile? profile)
    {
        _activeHrtfProfile = profile;
        var hrtf = HrtfSpatializer;
        if (hrtf is null) return;

        if (profile is null || string.Equals(profile.Id, "none", StringComparison.OrdinalIgnoreCase))
        {
            hrtf.IsEnabled = false;
            hrtf.SetProfile(null);
            return;
        }

        // Prepare profile for current DSP sample rate (resamples HRIRs if needed)
        var prepared = ProfilePreparer.Prepare(profile, _currentSampleRate);

        hrtf.IsEnabled = true;
        hrtf.SetProfile(prepared);
        // Default to center-front direction
        hrtf.SetDirection(0, 0);
    }

    /// <summary>
    /// Bypasses gaming enhancement by disabling all effects in the chain.
    /// Does NOT modify any voice-changer effects.
    /// </summary>
    public void Bypass()
    {
        _activeProfile = null;
        foreach (var effect in Chain.Effects)
        {
            effect.IsEnabled = false;
        }
    }

    /// <summary>
    /// Updates the sample rate on all effects in the chain.
    /// If an HRTF profile is active, re-prepares it for the new sample rate.
    /// Call this when the audio pipeline sample rate is known.
    /// </summary>
    public void SetSampleRate(int sampleRate)
    {
        _currentSampleRate = sampleRate;

        foreach (var effect in Chain.Effects)
        {
            switch (effect)
            {
                case EqualizerEffect eq:
                    eq.SampleRate = sampleRate;
                    break;
                case HrtfEffect hrtf:
                    hrtf.SampleRate = sampleRate;
                    break;
                case NoiseGateEffect gate:
                    gate.SampleRate = sampleRate;
                    break;
                case CompressorEffect comp:
                    comp.SampleRate = sampleRate;
                    break;
                case LimiterEffect limiter:
                    limiter.SampleRate = sampleRate;
                    break;
            }
        }

        // Re-prepare and reapply the active HRTF profile for the new sample rate
        if (_activeHrtfProfile is not null
            && !string.Equals(_activeHrtfProfile.Id, "none", StringComparison.OrdinalIgnoreCase))
        {
            ApplyHrtfProfile(_activeHrtfProfile);
        }
    }
}
