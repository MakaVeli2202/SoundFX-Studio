using System.Linq;

namespace SoundFXStudio.Models;

/// <summary>
/// An HRTF profile containing spatial audio impulse response data.
/// Each profile represents a collection of HRIR measurements at various
/// azimuth/elevation positions for a specific subject or dataset.
///
/// This is independent of HeadphoneProfile (which is frequency-response EQ).
/// HRTF provides spatialization via convolution with head-related impulse responses.
/// </summary>
public sealed class HrtfProfile
{
    /// <summary>
    /// Unique identifier for this profile.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Display name (e.g., "Synthetic Front", "CIPIC Subject 003").
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Manufacturer or organization that produced the measurements.
    /// </summary>
    public string Manufacturer { get; init; } = string.Empty;

    /// <summary>
    /// Description of this profile.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Sample rate the HRIR data was recorded at.
    /// </summary>
    public int SampleRate { get; init; } = 48000;

    /// <summary>
    /// Number of taps (coefficients) per impulse response.
    /// All entries in this profile must have the same IR length.
    /// </summary>
    public int IrLength { get; init; } = 128;

    /// <summary>
    /// The measured/synthetic HRTF entries at various directions.
    /// </summary>
    public HrtfEntry[] Entries { get; init; } = Array.Empty<HrtfEntry>();

    /// <summary>
    /// Source of the data (e.g., "Synthetic", "CIPIC", "MIT KEMAR").
    /// </summary>
    public string DataSource { get; init; } = "Synthetic";

    /// <summary>
    /// License information for the data.
    /// </summary>
    public string License { get; init; } = string.Empty;

    /// <summary>
    /// Creates a deep copy of this profile including all HRIR arrays.
    /// </summary>
    public HrtfProfile Clone()
    {
        return new HrtfProfile
        {
            Id = Id,
            Name = Name,
            Manufacturer = Manufacturer,
            Description = Description,
            SampleRate = SampleRate,
            IrLength = IrLength,
            Entries = Entries.Select(e => e.Clone()).ToArray(),
            DataSource = DataSource,
            License = License
        };
    }

    /// <summary>
    /// Finds the entry nearest to the given direction using angular distance.
    /// Returns null if the profile has no entries.
    /// Azimuth is normalized to [-180, 180] before lookup.
    /// </summary>
    public HrtfEntry? GetEntryForDirection(double azimuthDeg, double elevationDeg)
    {
        if (Entries.Length == 0) return null;

        var normAz = NormalizeAzimuth(azimuthDeg);
        var normEl = Math.Clamp(elevationDeg, -90.0, 90.0);

        HrtfEntry? best = null;
        var bestDist = double.MaxValue;

        foreach (var entry in Entries)
        {
            var azDiff = NormalizeAzimuth(entry.AzimuthDeg) - normAz;
            // Wrap azimuth difference to shortest arc
            if (azDiff > 180.0) azDiff -= 360.0;
            else if (azDiff < -180.0) azDiff += 360.0;

            var elDiff = entry.ElevationDeg - normEl;
            var dist = azDiff * azDiff + elDiff * elDiff;

            if (dist < bestDist)
            {
                bestDist = dist;
                best = entry;
            }
        }

        return best;
    }

    /// <summary>
    /// Normalizes azimuth to the range [-180, 180].
    /// </summary>
    private static double NormalizeAzimuth(double degrees)
    {
        degrees %= 360.0;
        if (degrees > 180.0) degrees -= 360.0;
        else if (degrees < -180.0) degrees += 360.0;
        return degrees;
    }

    public override string ToString() => string.IsNullOrWhiteSpace(Name) ? Id : Name;
}
