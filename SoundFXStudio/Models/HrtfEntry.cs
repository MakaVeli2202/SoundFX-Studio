namespace SoundFXStudio.Models;

/// <summary>
/// A single HRTF measurement at a specific spatial direction.
/// Contains the left-ear and right-ear impulse responses (HRIRs)
/// for audio arriving from the given azimuth and elevation.
/// </summary>
public sealed class HrtfEntry
{
    /// <summary>
    /// Horizontal angle in degrees. -180 to 180 (0 = front, positive = left).
    /// </summary>
    public double AzimuthDeg { get; init; }

    /// <summary>
    /// Vertical angle in degrees. -90 to 90 (0 = ear level, positive = above).
    /// </summary>
    public double ElevationDeg { get; init; }

    /// <summary>
    /// Left-ear impulse response coefficients.
    /// </summary>
    public float[] LeftEarResponse { get; init; } = Array.Empty<float>();

    /// <summary>
    /// Right-ear impulse response coefficients.
    /// </summary>
    public float[] RightEarResponse { get; init; } = Array.Empty<float>();

    /// <summary>
    /// Creates a deep copy of this entry.
    /// </summary>
    public HrtfEntry Clone()
    {
        var leftCopy = new float[LeftEarResponse.Length];
        var rightCopy = new float[RightEarResponse.Length];
        Array.Copy(LeftEarResponse, leftCopy, LeftEarResponse.Length);
        Array.Copy(RightEarResponse, rightCopy, RightEarResponse.Length);

        return new HrtfEntry
        {
            AzimuthDeg = AzimuthDeg,
            ElevationDeg = ElevationDeg,
            LeftEarResponse = leftCopy,
            RightEarResponse = rightCopy
        };
    }
}
