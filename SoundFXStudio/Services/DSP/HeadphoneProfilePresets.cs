using SoundFXStudio.Models;

namespace SoundFXStudio.Services.DSP;

/// <summary>
/// Built-in headphone EQ profiles.
/// Each profile provides AutoEq-style parametric filters through existing EqualizerEffect.
///
/// NOTE: The profiles below are TEST/EXAMPLE data only. They are clearly marked
/// and do NOT represent actual AutoEq measurements for specific headphone models.
/// To add real AutoEq profiles, replace the filter values with actual measurement data
/// from the AutoEq project (https://github.com/jaakkopasanen/AutoEq).
/// </summary>
public static class HeadphoneProfilePresets
{
    public static readonly IReadOnlyList<HeadphoneProfile> Profiles = new List<HeadphoneProfile>
    {
        CreateFlatTest(),
        CreateGentleBassTest(),
        CreateGentleTrebleTest(),
        CreateNeutralTest()
    };

    /// <summary>
    /// Returns a deep clone of the profile with the given ID,
    /// or a clone of the first profile if not found.
    /// </summary>
    public static HeadphoneProfile GetById(string id)
    {
        return (Profiles.FirstOrDefault(p => p.Id == id) ?? Profiles[0]).Clone();
    }

    /// <summary>
    /// Returns a "None" profile that disables headphone EQ (empty filter set).
    /// </summary>
    public static HeadphoneProfile GetNone()
    {
        return new HeadphoneProfile
        {
            Id = "none",
            Name = "None (Disabled)",
            Description = "Headphone EQ is disabled. No filters applied.",
            PreampDb = 0
        };
    }

    /// <summary>
    /// TEST profile: Flat response (all gains at 0 dB).
    /// This is a placeholder, NOT real AutoEq data.
    /// </summary>
    private static HeadphoneProfile CreateFlatTest()
    {
        return new HeadphoneProfile
        {
            Id = "flat-test",
            Name = "Flat (Test Example)",
            Manufacturer = "Example",
            Model = "Flat Reference",
            Description = "TEST EXAMPLE — Not real AutoEq data. All gains at 0 dB for verification.",
            PreampDb = 0,
            Filters =
            {
                new EqFilter { Type = EqFilterType.Peaking, FrequencyHz = 100, GainDb = 0, Q = 1.0 },
                new EqFilter { Type = EqFilterType.Peaking, FrequencyHz = 1000, GainDb = 0, Q = 1.0 },
                new EqFilter { Type = EqFilterType.Peaking, FrequencyHz = 10000, GainDb = 0, Q = 1.0 }
            }
        };
    }

    /// <summary>
    /// TEST profile: Gentle bass boost.
    /// This is a placeholder, NOT real AutoEq data.
    /// </summary>
    private static HeadphoneProfile CreateGentleBassTest()
    {
        return new HeadphoneProfile
        {
            Id = "gentle-bass-test",
            Name = "Gentle Bass (Test Example)",
            Manufacturer = "Example",
            Model = "Bass Boosted",
            Description = "TEST EXAMPLE — Not real AutoEq data. Simple low-frequency boost for verification.",
            PreampDb = -1,
            Filters =
            {
                new EqFilter { Type = EqFilterType.LowShelf, FrequencyHz = 100, GainDb = 3, Q = 0.7 },
                new EqFilter { Type = EqFilterType.Peaking, FrequencyHz = 60, GainDb = 2, Q = 1.0 }
            }
        };
    }

    /// <summary>
    /// TEST profile: Gentle treble boost.
    /// This is a placeholder, NOT real AutoEq data.
    /// </summary>
    private static HeadphoneProfile CreateGentleTrebleTest()
    {
        return new HeadphoneProfile
        {
            Id = "gentle-treble-test",
            Name = "Gentle Treble (Test Example)",
            Manufacturer = "Example",
            Model = "Treble Boosted",
            Description = "TEST EXAMPLE — Not real AutoEq data. Simple high-frequency boost for verification.",
            PreampDb = -1,
            Filters =
            {
                new EqFilter { Type = EqFilterType.Peaking, FrequencyHz = 4000, GainDb = 2, Q = 1.2 },
                new EqFilter { Type = EqFilterType.HighShelf, FrequencyHz = 8000, GainDb = 3, Q = 0.7 }
            }
        };
    }

    /// <summary>
    /// TEST profile: Neutral with slight mid cut.
    /// This is a placeholder, NOT real AutoEq data.
    /// </summary>
    private static HeadphoneProfile CreateNeutralTest()
    {
        return new HeadphoneProfile
        {
            Id = "neutral-test",
            Name = "Neutral (Test Example)",
            Manufacturer = "Example",
            Model = "Neutral Reference",
            Description = "TEST EXAMPLE — Not real AutoEq data. Slight midrange cut for verification.",
            PreampDb = 0,
            Filters =
            {
                new EqFilter { Type = EqFilterType.HighPass, FrequencyHz = 30, Q = 0.5 },
                new EqFilter { Type = EqFilterType.Peaking, FrequencyHz = 500, GainDb = -2, Q = 1.0 },
                new EqFilter { Type = EqFilterType.Peaking, FrequencyHz = 3000, GainDb = 1, Q = 1.2 },
                new EqFilter { Type = EqFilterType.LowPass, FrequencyHz = 18000, Q = 0.7 }
            }
        };
    }
}
