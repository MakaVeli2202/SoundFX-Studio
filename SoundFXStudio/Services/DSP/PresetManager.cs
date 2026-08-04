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
            NoiseGateEnabled = true, NoiseGateThresholdDb = -45f,
            LimiterEnabled = true
        },
        new()
        {
            Id = "deepmale", Name = "Deep Male",
            PitchSemitones = -3f, FormantShift = 0.85f,
            NoiseGateEnabled = true, NoiseGateThresholdDb = -45f,
            CompressorEnabled = true, CompressorThresholdDb = -20f, CompressorRatio = 4f,
            LimiterEnabled = true
        },
        new()
        {
            Id = "female", Name = "Female",
            PitchSemitones = 4f, FormantShift = 1.2f,
            NoiseGateEnabled = true, NoiseGateThresholdDb = -45f,
            CompressorEnabled = true, CompressorThresholdDb = -18f, CompressorRatio = 3f,
            LimiterEnabled = true
        },
        new()
        {
            Id = "robot", Name = "Robot",
            PitchSemitones = 0f, FormantShift = 1.5f,
            NoiseGateEnabled = true, NoiseGateThresholdDb = -45f,
            RobotEnabled = true, RobotFrequencyHz = 30f,
            LimiterEnabled = true
        },
        new()
        {
            Id = "demon", Name = "Demon",
            PitchSemitones = -5f, FormantShift = 0.7f,
            NoiseGateEnabled = true, NoiseGateThresholdDb = -45f,
            DistortionEnabled = true, DistortionDrive = 5f,
            LimiterEnabled = true
        },
        new()
        {
            Id = "anime", Name = "Anime",
            PitchSemitones = 7f, FormantShift = 1.35f,
            NoiseGateEnabled = true, NoiseGateThresholdDb = -45f,
            ChorusEnabled = true, ChorusMix = 0.3f,
            LimiterEnabled = true
        },
        new()
        {
            Id = "caveecho", Name = "Cave Echo",
            PitchSemitones = 1f, FormantShift = 1f,
            NoiseGateEnabled = true, NoiseGateThresholdDb = -45f,
            ReverbEnabled = true, ReverbMix = 0.45f, ReverbRoomSize = 0.85f,
            LimiterEnabled = true
        },
        new()
        {
            Id = "chipmunk", Name = "Chipmunk",
            PitchSemitones = 8f, FormantShift = 1.45f,
            NoiseGateEnabled = true, NoiseGateThresholdDb = -45f,
            LimiterEnabled = true
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
    }
}
