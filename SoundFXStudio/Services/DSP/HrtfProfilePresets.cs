using SoundFXStudio.Models;

namespace SoundFXStudio.Services.DSP;

/// <summary>
/// Built-in HRTF profiles with clearly labeled synthetic impulse responses.
///
/// IMPORTANT: These are SYNTHETIC/TEST profiles only. They do NOT represent
/// real-world HRTF measurements from any dataset (CIPIC, MIT KEMAR, ARI, Listen, etc.).
/// The impulse responses are deliberately simple and deterministic for DSP validation.
///
/// To add real HRTF profiles, load data from a legitimate open dataset with
/// appropriate licensing (e.g., MIT KEMAR CC license, CIPIC research license).
/// </summary>
public static class HrtfProfilePresets
{
    private const int DefaultIrLength = 32;
    private const int DefaultSampleRate = 48000;

    public static readonly IReadOnlyList<HrtfProfile> Profiles = new List<HrtfProfile>
    {
        CreateSyntheticFront(),
        CreateSyntheticAbove(),
        CreateSyntheticLeft()
    };

    /// <summary>
    /// Returns a deep clone of the profile with the given ID,
    /// or a clone of the first profile if not found.
    /// </summary>
    public static HrtfProfile GetById(string id)
    {
        return (Profiles.FirstOrDefault(p => p.Id == id) ?? Profiles[0]).Clone();
    }

    /// <summary>
    /// Returns the "None" profile that disables HRTF spatialization.
    /// </summary>
    public static HrtfProfile GetNone()
    {
        return NoneProfile.Clone();
    }

    private static readonly HrtfProfile NoneProfile = new()
    {
        Id = "none",
        Name = "Disabled",
        Manufacturer = "N/A",
        Description = "HRTF spatialization disabled. Audio passes through unprocessed.",
        SampleRate = DefaultSampleRate,
        IrLength = 0,
        Entries = Array.Empty<HrtfEntry>(),
        DataSource = "None",
        License = "N/A"
    };

    /// <summary>
    /// SYNTHETIC profile: sound source directly in front.
    /// Both ears receive the same impulse (no ITD, no ILD difference).
    /// Useful for verifying symmetric processing.
    /// </summary>
    private static HrtfProfile CreateSyntheticFront()
    {
        var leftIr = new float[DefaultIrLength];
        var rightIr = new float[DefaultIrLength];

        // Unity gain impulse at tap 0 for both ears (front = symmetric)
        leftIr[0] = 1.0f;
        rightIr[0] = 1.0f;

        return new HrtfProfile
        {
            Id = "synthetic-front",
            Name = "Synthetic Front",
            Manufacturer = "Synthetic",
            Description = "SYNTHETIC TEST DATA — Not real HRTF. Sound source directly in front (0° azimuth, 0° elevation). Both ears receive identical unity-gain impulse. Useful for verifying symmetric processing.",
            SampleRate = DefaultSampleRate,
            IrLength = DefaultIrLength,
            Entries = new[]
            {
                new HrtfEntry
                {
                    AzimuthDeg = 0,
                    ElevationDeg = 0,
                    LeftEarResponse = leftIr,
                    RightEarResponse = rightIr
                }
            },
            DataSource = "Synthetic — Test/Validation Only",
            License = "N/A — Not a real dataset"
        };
    }

    /// <summary>
    /// SYNTHETIC profile: sound source from above.
    /// Left ear gets a delayed, attenuated impulse.
    /// Right ear gets a slightly different delay/attenuation.
    /// Tests elevation-dependent processing.
    /// </summary>
    private static HrtfProfile CreateSyntheticAbove()
    {
        var leftIr = new float[DefaultIrLength];
        var rightIr = new float[DefaultIrLength];

        // Left ear: delay of 2 taps, gain 0.8
        leftIr[2] = 0.8f;
        // Right ear: delay of 2 taps, gain 0.7 (slightly different for elevation cue)
        rightIr[2] = 0.7f;

        return new HrtfProfile
        {
            Id = "synthetic-above",
            Name = "Synthetic Above",
            Manufacturer = "Synthetic",
            Description = "SYNTHETIC TEST DATA — Not real HRTF. Sound source from above (0° azimuth, +90° elevation). Left/right ears have different gains to test elevation-dependent processing.",
            SampleRate = DefaultSampleRate,
            IrLength = DefaultIrLength,
            Entries = new[]
            {
                new HrtfEntry
                {
                    AzimuthDeg = 0,
                    ElevationDeg = 90,
                    LeftEarResponse = leftIr,
                    RightEarResponse = rightIr
                }
            },
            DataSource = "Synthetic — Test/Validation Only",
            License = "N/A — Not a real dataset"
        };
    }

    /// <summary>
    /// SYNTHETIC profile: sound source from the left.
    /// Left ear: immediate impulse, unity gain.
    /// Right ear: delayed impulse, attenuated (shadowed by head).
    /// Tests ITD (interaural time difference) and ILD (interaural level difference).
    /// </summary>
    private static HrtfProfile CreateSyntheticLeft()
    {
        var leftIr = new float[DefaultIrLength];
        var rightIr = new float[DefaultIrLength];

        // Left ear: direct, no delay, unity gain
        leftIr[0] = 1.0f;
        // Right ear: delayed by 3 taps, attenuated to 0.5 (head shadow)
        rightIr[3] = 0.5f;

        return new HrtfProfile
        {
            Id = "synthetic-left",
            Name = "Synthetic Left",
            Manufacturer = "Synthetic",
            Description = "SYNTHETIC TEST DATA — Not real HRTF. Sound source from the left (-90° azimuth, 0° elevation). Tests ITD (3-sample delay) and ILD (0.5 attenuation on right ear).",
            SampleRate = DefaultSampleRate,
            IrLength = DefaultIrLength,
            Entries = new[]
            {
                new HrtfEntry
                {
                    AzimuthDeg = -90,
                    ElevationDeg = 0,
                    LeftEarResponse = leftIr,
                    RightEarResponse = rightIr
                }
            },
            DataSource = "Synthetic — Test/Validation Only",
            License = "N/A — Not a real dataset"
        };
    }
}
