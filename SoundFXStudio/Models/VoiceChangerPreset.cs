namespace SoundFXStudio.Models;

public class VoiceChangerPreset
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public float PitchSemitones { get; set; }

    public float FormantShift { get; set; } = 1f;

    public bool NoiseGateEnabled { get; set; } = true;

    public float NoiseGateThresholdDb { get; set; } = -45f;

    public bool CompressorEnabled { get; set; }

    public float CompressorThresholdDb { get; set; } = -20f;

    public float CompressorRatio { get; set; } = 4f;

    public bool LimiterEnabled { get; set; } = true;

    public bool DistortionEnabled { get; set; }

    public float DistortionDrive { get; set; } = 4f;

    public bool ReverbEnabled { get; set; }

    public float ReverbMix { get; set; } = 0.35f;

    public float ReverbRoomSize { get; set; } = 0.7f;

    public bool RobotEnabled { get; set; }

    public float RobotFrequencyHz { get; set; } = 30f;

    public bool ChorusEnabled { get; set; }

    public float ChorusMix { get; set; } = 0.4f;
}
