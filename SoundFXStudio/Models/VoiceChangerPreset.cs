namespace SoundFXStudio.Models;

public class VoiceChangerPreset
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public override string ToString() => string.IsNullOrWhiteSpace(Name) ? Id : Name;

    public float PitchSemitones { get; set; }

    public float FormantShift { get; set; } = 1f;

    public bool NoiseGateEnabled { get; set; } = true;

    public float NoiseGateThresholdDb { get; set; } = -40f;

    public bool CompressorEnabled { get; set; } = true;

    public float CompressorThresholdDb { get; set; } = -18f;

    public float CompressorRatio { get; set; } = 3f;

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

    public bool ClarityEnabled { get; set; } = true;

    public double ClarityHpfHz { get; set; } = 80;

    public double ClarityPresenceDb { get; set; } = 3.0;

    public double ClarityPresenceHz { get; set; } = 3000;

    public double ClarityAirDb { get; set; } = 2.0;

    public double ClarityAirHz { get; set; } = 8000;
}
