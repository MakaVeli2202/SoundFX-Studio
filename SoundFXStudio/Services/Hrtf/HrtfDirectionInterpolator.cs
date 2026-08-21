using System;
using SoundFXStudio.Models;

namespace SoundFXStudio.Services.Hrtf;

/// <summary>
/// Performs HRTF direction interpolation using inverse-distance weighting (IDW)
/// over the nearest neighbor entries.
///
/// Strategy: For an arbitrary query direction (azimuth, elevation), find the K
/// nearest measurement entries by angular distance, compute weights proportional
/// to 1/distance², and produce an interpolated HRIR as the weighted sum of
/// the source HRIRs sample-by-sample.
///
/// This handles:
/// - Irregular/non-uniform SOFA measurement grids
/// - Varying azimuth spacing per elevation
/// - Azimuth wraparound at ±180°
/// - Elevation boundaries (-90° to +90°)
/// - Sparse grids (falls back to nearest-neighbor when too few neighbors exist)
///
/// All interpolation occurs OUTSIDE Process(). The interpolator returns
/// newly-allocated float arrays; these are then copied into preallocated
/// buffers inside HrtfEffect.
/// </summary>
public static class HrtfDirectionInterpolator
{
    /// <summary>
    /// Maximum number of neighbors used for interpolation.
    /// 4 is a good balance between quality and cost for typical SOFA grids.
    /// </summary>
    private const int MaxNeighbors = 4;

    /// <summary>
    /// Minimum angular distance (degrees) below which we use the exact entry
    /// without interpolation, avoiding division-by-zero in weight computation.
    /// </summary>
    private const double ExactMatchThreshold = 0.01;

    /// <summary>
    /// Interpolates left and right ear HRIRs for the given direction.
    /// Returns a tuple of (leftHrir, rightHrir) arrays.
    /// Both arrays are newly allocated and have identical length.
    ///
    /// Falls back to nearest-neighbor when:
    /// - profile has no entries
    /// - only 1 entry exists
    /// - query direction exactly matches an entry
    /// - too few valid neighbors for weighted interpolation
    /// </summary>
    public static (float[] Left, float[] Right) Interpolate(
        HrtfProfile profile,
        double azimuthDeg,
        double elevationDeg)
    {
        var entries = profile.Entries;
        if (entries.Length == 0)
            return (Array.Empty<float>(), Array.Empty<float>());

        if (entries.Length == 1)
            return CopyHrir(entries[0]);

        var normAz = NormalizeAzimuth(azimuthDeg);
        var normEl = Math.Clamp(elevationDeg, -90.0, 90.0);

        // Find nearest neighbors sorted by angular distance
        Span<(int index, double distance)> neighbors =
            stackalloc (int index, double distance)[Math.Min(MaxNeighbors + 1, entries.Length)];

        int count = FindNearestNeighbors(entries, normAz, normEl, neighbors);

        // Exact match — use that entry directly
        if (count > 0 && neighbors[0].distance < ExactMatchThreshold)
            return CopyHrir(entries[neighbors[0].index]);

        // Need at least 2 neighbors for interpolation
        if (count < 2)
            return CopyHrir(entries[neighbors[0].index]);

        // Compute IDW weights
        Span<double> weights = stackalloc double[count];
        ComputeWeights(neighbors, count, weights);

        // Determine output HRIR length (must be consistent across all source entries)
        var irLength = profile.IrLength;
        if (irLength <= 0)
            return (Array.Empty<float>(), Array.Empty<float>());

        // Interpolate
        var left = new float[irLength];
        var right = new float[irLength];

        for (int i = 0; i < count; i++)
        {
            var entry = entries[neighbors[i].index];
            var w = weights[i];
            var len = Math.Min(irLength, entry.LeftEarResponse.Length);
            var rLen = Math.Min(irLength, entry.RightEarResponse.Length);

            for (int s = 0; s < len; s++)
                left[s] += (float)(w * entry.LeftEarResponse[s]);

            for (int s = 0; s < rLen; s++)
                right[s] += (float)(w * entry.RightEarResponse[s]);
        }

        return (left, right);
    }

    /// <summary>
    /// Finds the nearest neighbors to the query direction.
    /// Fills the span up to MaxNeighbors entries, sorted by increasing distance.
    /// Returns the number of neighbors found.
    /// </summary>
    private static int FindNearestNeighbors(
        HrtfEntry[] entries,
        double queryAz,
        double queryEl,
        Span<(int index, double distance)> neighbors)
    {
        int count = 0;
        var worstDistance = double.MaxValue;
        var worstIndex = 0;

        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            var azDiff = NormalizeAzimuth(entry.AzimuthDeg) - queryAz;
            // Wrap to shortest arc
            if (azDiff > 180.0) azDiff -= 360.0;
            else if (azDiff < -180.0) azDiff += 360.0;

            var elDiff = entry.ElevationDeg - queryEl;
            var dist = Math.Sqrt(azDiff * azDiff + elDiff * elDiff);

            if (count < MaxNeighbors)
            {
                neighbors[count] = (i, dist);
                count++;

                // Track worst in the partial sorted list
                if (dist > worstDistance)
                {
                    worstDistance = dist;
                    worstIndex = count - 1;
                }
            }
            else if (dist < worstDistance)
            {
                // Replace worst neighbor
                neighbors[worstIndex] = (i, dist);

                // Re-find worst
                worstDistance = double.MaxValue;
                for (int j = 0; j < count; j++)
                {
                    if (neighbors[j].distance > worstDistance)
                    {
                        worstDistance = neighbors[j].distance;
                        worstIndex = j;
                    }
                }
            }
        }

        // Sort by distance (small insertion sort — tiny array)
        for (int i = 1; i < count; i++)
        {
            var key = neighbors[i];
            int j = i - 1;
            while (j >= 0 && neighbors[j].distance > key.distance)
            {
                neighbors[j + 1] = neighbors[j];
                j--;
            }
            neighbors[j + 1] = key;
        }

        return count;
    }

    /// <summary>
    /// Computes inverse-distance-squared weights, normalized to sum to 1.0.
    /// </summary>
    private static void ComputeWeights(
        Span<(int index, double distance)> neighbors,
        int count,
        Span<double> weights)
    {
        double sum = 0;
        for (int i = 0; i < count; i++)
        {
            var d = neighbors[i].distance;
            // Prevent division by zero for very close neighbors
            var w = d < ExactMatchThreshold ? 1e12 : 1.0 / (d * d);
            weights[i] = w;
            sum += w;
        }

        // Normalize
        if (sum > 0)
        {
            for (int i = 0; i < count; i++)
                weights[i] /= sum;
        }
        else
        {
            // Fallback: equal weights (shouldn't happen in practice)
            var equal = 1.0 / count;
            for (int i = 0; i < count; i++)
                weights[i] = equal;
        }
    }

    private static (float[] Left, float[] Right) CopyHrir(HrtfEntry entry)
    {
        var left = new float[entry.LeftEarResponse.Length];
        var right = new float[entry.RightEarResponse.Length];
        Array.Copy(entry.LeftEarResponse, left, left.Length);
        Array.Copy(entry.RightEarResponse, right, right.Length);
        return (left, right);
    }

    private static double NormalizeAzimuth(double degrees)
    {
        degrees %= 360.0;
        if (degrees > 180.0) degrees -= 360.0;
        else if (degrees < -180.0) degrees += 360.0;
        return degrees;
    }
}
