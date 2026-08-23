using SoundFXStudio.Models;

namespace SoundFXStudio.Services.DSP;

public static class PresetManager
{
    public static readonly IReadOnlyList<VoiceChangerPreset> Presets = new List<VoiceChangerPreset>
    {
        new()
        {
            Id = "normal", Name = "Normal",
            PitchSemitones = 0, FormantShift = 1f,
            NoiseGateEnabled = true, NoiseGateThresholdDb = -40f,
            CompressorEnabled = true, CompressorThresholdDb = -18f, CompressorRatio = 3f,
            LimiterEnabled = true,
            ClarityEnabled = true, ClarityHpfHz = 80,
            ClarityPresenceDb = 3.0, ClarityPresenceHz = 3000,
            ClarityAirDb = 2.0, ClarityAirHz = 8000
        },
        new()
        {
            Id = "deepmale", Name = "Deep Male",
            PitchSemitones = -4f, FormantShift = 0.9f,
            NoiseGateEnabled = true, NoiseGateThresholdDb = -40f,
            CompressorEnabled = true, CompressorThresholdDb = -18f, CompressorRatio = 3f,
            LimiterEnabled = true,
            ClarityEnabled = true, ClarityHpfHz = 80,
            ClarityPresenceDb = 3.0, ClarityPresenceHz = 3000,
            ClarityAirDb = 2.0, ClarityAirHz = 8000
        },
        new()
        {
            Id = "female", Name = "Female",
            PitchSemitones = 5f, FormantShift = 1.2f,
            NoiseGateEnabled = true, NoiseGateThresholdDb = -40f,
            CompressorEnabled = true, CompressorThresholdDb = -18f, CompressorRatio = 3f,
            LimiterEnabled = true,
            ClarityEnabled = true, ClarityHpfHz = 80,
            ClarityPresenceDb = 2.5, ClarityPresenceHz = 3500,
            ClarityAirDb = 2.5, ClarityAirHz = 9000
        },
        new()
        {
            Id = "robot", Name = "Robot",
            PitchSemitones = 0f, FormantShift = 1f,
            NoiseGateEnabled = true, NoiseGateThresholdDb = -40f,
            CompressorEnabled = true, CompressorThresholdDb = -18f, CompressorRatio = 3f,
            RobotEnabled = true, RobotFrequencyHz = 120f,
            LimiterEnabled = true,
            ClarityEnabled = true, ClarityHpfHz = 80,
            ClarityPresenceDb = 3.0, ClarityPresenceHz = 3000,
            ClarityAirDb = 2.0, ClarityAirHz = 8000
        },
        new()
        {
            Id = "demon", Name = "Demon",
            PitchSemitones = -6f, FormantShift = 0.8f,
            NoiseGateEnabled = true, NoiseGateThresholdDb = -40f,
            CompressorEnabled = true, CompressorThresholdDb = -18f, CompressorRatio = 3f,
            DistortionEnabled = true, DistortionDrive = 5f,
            LimiterEnabled = true,
            ClarityEnabled = true, ClarityHpfHz = 80,
            ClarityPresenceDb = 2.0, ClarityPresenceHz = 2500,
            ClarityAirDb = 1.5, ClarityAirHz = 6000
        },
        new()
        {
            Id = "anime", Name = "Anime",
            PitchSemitones = 6f, FormantShift = 1.2f,
            NoiseGateEnabled = true, NoiseGateThresholdDb = -40f,
            CompressorEnabled = true, CompressorThresholdDb = -18f, CompressorRatio = 3f,
            ChorusEnabled = true, ChorusMix = 0.3f,
            LimiterEnabled = true,
            ClarityEnabled = true, ClarityHpfHz = 80,
            ClarityPresenceDb = 3.0, ClarityPresenceHz = 4000,
            ClarityAirDb = 3.0, ClarityAirHz = 9000
        },
        new()
        {
            Id = "caveecho", Name = "Cave Echo",
            PitchSemitones = 1f, FormantShift = 1f,
            NoiseGateEnabled = true, NoiseGateThresholdDb = -40f,
            CompressorEnabled = true, CompressorThresholdDb = -18f, CompressorRatio = 3f,
            ReverbEnabled = true, ReverbMix = 0.45f, ReverbRoomSize = 0.85f,
            LimiterEnabled = true,
            ClarityEnabled = true, ClarityHpfHz = 80,
            ClarityPresenceDb = 2.5, ClarityPresenceHz = 3000,
            ClarityAirDb = 2.0, ClarityAirHz = 8000
        },
        new()
        {
            Id = "chipmunk", Name = "Chipmunk",
            PitchSemitones = 8f, FormantShift = 1.3f,
            NoiseGateEnabled = true, NoiseGateThresholdDb = -40f,
            CompressorEnabled = true, CompressorThresholdDb = -18f, CompressorRatio = 3f,
            LimiterEnabled = true,
            ClarityEnabled = true, ClarityHpfHz = 80,
            ClarityPresenceDb = 3.0, ClarityPresenceHz = 4000,
            ClarityAirDb = 3.0, ClarityAirHz = 10000
        }
    };

    public static VoiceChangerPreset GetById(string id)
    {
        return Presets.FirstOrDefault(p => p.Id == id) ?? Presets[0];
    }

    public static void Apply(VoiceChangerPreset preset, VoiceChangerService service)
    {
        service.SetPitch(preset.PitchSemitones);
        service.SetFormant(preset.FormantShift);

        service.Chain.Get<NoiseGateEffect>()!.IsEnabled = preset.NoiseGateEnabled;
        service.Chain.Get<NoiseGateEffect>()!.ThresholdDb = preset.NoiseGateThresholdDb;
        service.Chain.Get<CompressorEffect>()!.IsEnabled = preset.CompressorEnabled;
        service.Chain.Get<CompressorEffect>()!.ThresholdDb = preset.CompressorThresholdDb;
        service.Chain.Get<CompressorEffect>()!.Ratio = preset.CompressorRatio;
        service.Chain.Get<LimiterEffect>()!.IsEnabled = preset.LimiterEnabled;
        service.Chain.Get<DistortionEffect>()!.IsEnabled = preset.DistortionEnabled;
        service.Chain.Get<DistortionEffect>()!.Drive = preset.DistortionDrive;
        service.Chain.Get<ReverbEffect>()!.IsEnabled = preset.ReverbEnabled;
        service.Chain.Get<ReverbEffect>()!.Mix = preset.ReverbMix;
        service.Chain.Get<ReverbEffect>()!.RoomSize = preset.ReverbRoomSize;
        service.Chain.Get<RobotEffect>()!.IsEnabled = preset.RobotEnabled;
        service.Chain.Get<RobotEffect>()!.FrequencyHz = preset.RobotFrequencyHz;
        service.Chain.Get<ChorusEffect>()!.IsEnabled = preset.ChorusEnabled;
        service.Chain.Get<ChorusEffect>()!.Mix = preset.ChorusMix;

        var eq = service.Chain.Get<EqualizerEffect>();
        if (eq is not null)
        {
            eq.IsEnabled = preset.ClarityEnabled;
            eq.SetFilters(new List<EqFilter>
            {
                new()
                {
                    Type = EqFilterType.HighPass,
                    FrequencyHz = preset.ClarityHpfHz,
                    Q = 0.7,
                    Enabled = preset.ClarityEnabled
                },
                new()
                {
                    Type = EqFilterType.Peaking,
                    FrequencyHz = preset.ClarityPresenceHz,
                    GainDb = preset.ClarityPresenceDb,
                    Q = 1.2,
                    Enabled = preset.ClarityEnabled
                },
                new()
                {
                    Type = EqFilterType.Peaking,
                    FrequencyHz = preset.ClarityAirHz,
                    GainDb = preset.ClarityAirDb,
                    Q = 0.8,
                    Enabled = preset.ClarityEnabled
                }
            });
        }
    }
}
