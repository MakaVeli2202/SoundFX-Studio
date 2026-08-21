using SoundFXStudio.Models;

namespace SoundFXStudio.Services.DSP;

/// <summary>
/// Built-in gaming audio enhancement profiles.
/// Each profile configures existing DSP effects through data, not code duplication.
/// Values are conservative starting points that can be tuned later.
/// </summary>
public static class GamingProfilePresets
{
    public static readonly IReadOnlyList<GamingProfile> Profiles = new List<GamingProfile>
    {
        CreateCompetitiveFps(),
        CreateFootstepFocus(),
        CreateDirectionalAudio(),
        CreateVoiceFocus(),
        CreateImmersive(),
        CreateMovieCinematic()
    };

    public static GamingProfile GetById(string id)
    {
        return (Profiles.FirstOrDefault(p => p.Id == id) ?? Profiles[0]).Clone();
    }

    private static GamingProfile CreateCompetitiveFps()
    {
        return new GamingProfile
        {
            Id = "competitive-fps",
            Name = "Competitive FPS",
            Description = "Reduce masking, improve intelligibility, preserve positional cues, controlled dynamics.",
            Category = "Gaming",
            IsEnabled = true,
            PreampDb = -1,
            NoiseGateEnabled = false,
            CompressorEnabled = true,
            CompressorThresholdDb = -18,
            CompressorRatio = 3,
            CompressorAttackMs = 3,
            CompressorReleaseMs = 100,
            CompressorMakeUpGainDb = 2,
            LimiterEnabled = true,
            LimiterThreshold = 0.9,
            LimiterReleaseMs = 80,
            EqEnabled = true,
            EqFilters =
            {
                new EqFilter { Type = EqFilterType.HighPass, FrequencyHz = 80, Q = 0.7 },
                new EqFilter { Type = EqFilterType.Peaking, FrequencyHz = 200, GainDb = -3, Q = 1.0 },
                new EqFilter { Type = EqFilterType.Peaking, FrequencyHz = 1500, GainDb = 3, Q = 1.2 },
                new EqFilter { Type = EqFilterType.Peaking, FrequencyHz = 4000, GainDb = 2, Q = 1.5 },
                new EqFilter { Type = EqFilterType.HighShelf, FrequencyHz = 8000, GainDb = 1, Q = 0.7 }
            }
        };
    }

    private static GamingProfile CreateFootstepFocus()
    {
        return new GamingProfile
        {
            Id = "footstep-focus",
            Name = "Footstep Focus",
            Description = "Emphasize presence information, reduce masking, controlled compression, limiter protection.",
            Category = "Gaming",
            IsEnabled = true,
            PreampDb = -1,
            NoiseGateEnabled = false,
            CompressorEnabled = true,
            CompressorThresholdDb = -16,
            CompressorRatio = 4,
            CompressorAttackMs = 2,
            CompressorReleaseMs = 120,
            CompressorMakeUpGainDb = 3,
            LimiterEnabled = true,
            LimiterThreshold = 0.88,
            LimiterReleaseMs = 100,
            EqEnabled = true,
            EqFilters =
            {
                new EqFilter { Type = EqFilterType.HighPass, FrequencyHz = 100, Q = 0.7 },
                new EqFilter { Type = EqFilterType.LowShelf, FrequencyHz = 150, GainDb = -4, Q = 0.8 },
                new EqFilter { Type = EqFilterType.Peaking, FrequencyHz = 1000, GainDb = 3, Q = 1.0 },
                new EqFilter { Type = EqFilterType.Peaking, FrequencyHz = 2500, GainDb = 4, Q = 1.5 },
                new EqFilter { Type = EqFilterType.Peaking, FrequencyHz = 5000, GainDb = 3, Q = 1.2 },
                new EqFilter { Type = EqFilterType.HighShelf, FrequencyHz = 10000, GainDb = 1, Q = 0.7 }
            }
        };
    }

    private static GamingProfile CreateDirectionalAudio()
    {
        return new GamingProfile
        {
            Id = "directional-audio",
            Name = "Directional Audio",
            Description = "Preserve spatial and positional information, avoid excessive processing that destroys directional cues.",
            Category = "Gaming",
            IsEnabled = true,
            PreampDb = 0,
            NoiseGateEnabled = false,
            CompressorEnabled = false,
            LimiterEnabled = true,
            LimiterThreshold = 0.95,
            LimiterReleaseMs = 100,
            EqEnabled = true,
            EqFilters =
            {
                new EqFilter { Type = EqFilterType.HighPass, FrequencyHz = 60, Q = 0.5 },
                new EqFilter { Type = EqFilterType.Peaking, FrequencyHz = 1500, GainDb = 2, Q = 0.8 },
                new EqFilter { Type = EqFilterType.Peaking, FrequencyHz = 4000, GainDb = 2, Q = 1.0 },
                new EqFilter { Type = EqFilterType.Peaking, FrequencyHz = 8000, GainDb = 1, Q = 1.0 }
            }
        };
    }

    private static GamingProfile CreateVoiceFocus()
    {
        return new GamingProfile
        {
            Id = "voice-focus",
            Name = "Voice Focus",
            Description = "Improve speech intelligibility, emphasize speech-presence region, controlled compression.",
            Category = "Gaming",
            IsEnabled = true,
            PreampDb = -1,
            NoiseGateEnabled = true,
            NoiseGateThresholdDb = -42,
            CompressorEnabled = true,
            CompressorThresholdDb = -15,
            CompressorRatio = 3,
            CompressorAttackMs = 3,
            CompressorReleaseMs = 150,
            CompressorMakeUpGainDb = 2,
            LimiterEnabled = true,
            LimiterThreshold = 0.9,
            LimiterReleaseMs = 100,
            EqEnabled = true,
            EqFilters =
            {
                new EqFilter { Type = EqFilterType.HighPass, FrequencyHz = 80, Q = 0.7 },
                new EqFilter { Type = EqFilterType.LowShelf, FrequencyHz = 200, GainDb = -3, Q = 0.8 },
                new EqFilter { Type = EqFilterType.Peaking, FrequencyHz = 1000, GainDb = 3, Q = 1.0 },
                new EqFilter { Type = EqFilterType.Peaking, FrequencyHz = 3000, GainDb = 4, Q = 1.2 },
                new EqFilter { Type = EqFilterType.Peaking, FrequencyHz = 5000, GainDb = 2, Q = 1.0 },
                new EqFilter { Type = EqFilterType.HighShelf, FrequencyHz = 8000, GainDb = 1, Q = 0.7 }
            }
        };
    }

    private static GamingProfile CreateImmersive()
    {
        return new GamingProfile
        {
            Id = "immersive",
            Name = "Immersive",
            Description = "Fuller sound, less aggressive processing, preserve more cinematic information.",
            Category = "Gaming",
            IsEnabled = true,
            PreampDb = 0,
            NoiseGateEnabled = false,
            CompressorEnabled = false,
            LimiterEnabled = true,
            LimiterThreshold = 0.95,
            LimiterReleaseMs = 100,
            EqEnabled = true,
            EqFilters =
            {
                new EqFilter { Type = EqFilterType.LowShelf, FrequencyHz = 100, GainDb = 2, Q = 0.7 },
                new EqFilter { Type = EqFilterType.Peaking, FrequencyHz = 2000, GainDb = 1, Q = 0.8 },
                new EqFilter { Type = EqFilterType.HighShelf, FrequencyHz = 10000, GainDb = 2, Q = 0.7 }
            }
        };
    }

    private static GamingProfile CreateMovieCinematic()
    {
        return new GamingProfile
        {
            Id = "movie-cinematic",
            Name = "Movie / Cinematic",
            Description = "Natural and full sound, less competitive tuning, wider dynamic presentation.",
            Category = "Media",
            IsEnabled = true,
            PreampDb = 0,
            NoiseGateEnabled = false,
            CompressorEnabled = true,
            CompressorThresholdDb = -20,
            CompressorRatio = 2,
            CompressorAttackMs = 10,
            CompressorReleaseMs = 200,
            CompressorMakeUpGainDb = 1,
            LimiterEnabled = true,
            LimiterThreshold = 0.95,
            LimiterReleaseMs = 150,
            EqEnabled = true,
            EqFilters =
            {
                new EqFilter { Type = EqFilterType.LowShelf, FrequencyHz = 80, GainDb = 3, Q = 0.7 },
                new EqFilter { Type = EqFilterType.Peaking, FrequencyHz = 3000, GainDb = 1, Q = 1.0 },
                new EqFilter { Type = EqFilterType.HighShelf, FrequencyHz = 12000, GainDb = 2, Q = 0.7 }
            }
        };
    }
}
