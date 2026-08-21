using System;

namespace SoundFXStudio.Services.Hrtf;

/// <summary>
/// Resamples a mono HRIR (Head-Related Impulse Response) array from one sample rate to another
/// using cubic Hermite (Catmull-Rom) interpolation.
///
/// Algorithm: Catmull-Rom spline interpolation between sample points.
/// - Deterministic: same input always produces same output.
/// - No allocations in Resample() after initial output array creation.
/// - Preserves temporal duration of the impulse response.
/// - Output length = round(inputLength * targetRate / sourceRate).
///
/// Tradeoffs vs alternatives:
/// - Better than linear interpolation: smoother frequency response, less aliasing.
/// - Worse than windowed-sinc: may introduce slight ringing at sharp transients.
/// - Acceptable for HRIRs which are naturally smooth, band-limited signals.
/// </summary>
public static class HrirResampler
{
    private const double MinSampleRate = 8000;
    private const double MaxSampleRate = 192000;

    /// <summary>
    /// Resamples a mono HRIR from sourceRate to targetRate.
    /// Returns a newly allocated array of resampled coefficients.
    /// </summary>
    /// <param name="hrir">Input HRIR coefficients. Must not be null or empty.</param>
    /// <param name="sourceRate">Source sample rate in Hz. Must be in [8000, 192000].</param>
    /// <param name="targetRate">Target sample rate in Hz. Must be in [8000, 192000].</param>
    /// <returns>Resampled HRIR array.</returns>
    /// <exception cref="ArgumentNullException">hrir is null.</exception>
    /// <exception cref="ArgumentException">hrir is empty, or sample rates are invalid.</exception>
    public static float[] Resample(float[] hrir, int sourceRate, int targetRate)
    {
        if (hrir is null)
            throw new ArgumentNullException(nameof(hrir));
        if (hrir.Length == 0)
            throw new ArgumentException("HRIR must not be empty.", nameof(hrir));
        if (sourceRate < MinSampleRate || sourceRate > MaxSampleRate)
            throw new ArgumentOutOfRangeException(nameof(sourceRate),
                $"Source sample rate must be between {MinSampleRate} and {MaxSampleRate}.");
        if (targetRate < MinSampleRate || targetRate > MaxSampleRate)
            throw new ArgumentOutOfRangeException(nameof(targetRate),
                $"Target sample rate must be between {MinSampleRate} and {MaxSampleRate}.");

        // Same rate: return a copy (never share the original)
        if (sourceRate == targetRate)
        {
            var copy = new float[hrir.Length];
            Array.Copy(hrir, copy, hrir.Length);
            return copy;
        }

        // Calculate target length preserving temporal duration
        var targetLength = CalculateTargetLength(hrir.Length, sourceRate, targetRate);
        if (targetLength <= 0)
            return Array.Empty<float>();

        var result = new float[targetLength];
        var ratio = (double)sourceRate / targetRate;

        for (int i = 0; i < targetLength; i++)
        {
            // Source position for this output sample
            var srcPos = i * ratio;
            var srcIdx = (int)srcPos;
            var frac = srcPos - srcIdx;

            // Catmull-Rom cubic interpolation using 4 points
            // p0, p1 are the bracketing samples; p[-1] and p[2] provide slope
            var p0 = GetSample(hrir, srcIdx - 1);
            var p1 = GetSample(hrir, srcIdx);
            var p2 = GetSample(hrir, srcIdx + 1);
            var p3 = GetSample(hrir, srcIdx + 2);

            result[i] = (float)HermiteInterpolate(p0, p1, p2, p3, frac);
        }

        return result;
    }

    /// <summary>
    /// Calculates the target HRIR length preserving temporal duration.
    /// Formula: targetLength = round(sourceLength * targetRate / sourceRate).
    /// </summary>
    public static int CalculateTargetLength(int sourceLength, int sourceRate, int targetRate)
    {
        if (sourceLength <= 0 || sourceRate <= 0 || targetRate <= 0)
            return 0;

        return (int)Math.Round((double)sourceLength * targetRate / sourceRate);
    }

    /// <summary>
    /// Catmull-Rom cubic Hermite interpolation.
    /// t is in [0,1] between p1 and p2.
    /// </summary>
    private static double HermiteInterpolate(double p0, double p1, double p2, double p3, double t)
    {
        // Catmull-Rom tangent coefficients: m1 = (p2 - p0) / 2, m2 = (p3 - p1) / 2
        var m1 = (p2 - p0) * 0.5;
        var m2 = (p3 - p1) * 0.5;

        var t2 = t * t;
        var t3 = t2 * t;

        // Hermite basis functions
        var h00 = 2 * t3 - 3 * t2 + 1;
        var h10 = t3 - 2 * t2 + t;
        var h01 = -2 * t3 + 3 * t2;
        var h11 = t3 - t2;

        return h00 * p1 + h10 * m1 + h01 * p2 + h11 * m2;
    }

    /// <summary>
    /// Gets a sample from the HRIR array with zero-extension at boundaries.
    /// </summary>
    private static double GetSample(float[] hrir, int index)
    {
        if (index < 0 || index >= hrir.Length)
            return 0.0;
        return hrir[index];
    }
}
