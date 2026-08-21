namespace SoundFXStudio.Services.Hrtf;

/// <summary>
/// Converts raw head orientation to HRTF azimuth/elevation,
/// applying calibration offset and coordinate normalization.
///
/// HRTF coordinate system:
///   Azimuth: 0° = front, positive = left, negative = right, range -180°..+180°
///   Elevation: 0° = horizontal, positive = above, negative = below
///
/// Head tracking convention:
///   Yaw: positive = head turned left (maps to positive azimuth)
///   Pitch: positive = head tilted up (maps to positive elevation)
///   Roll: positive = tilt to left shoulder (NOT used by HRTF, reserved)
///
/// Calibration stores the reference orientation. After calibration,
/// relativeYaw = rawYaw - calibratedYaw, etc.
/// </summary>
public sealed class HeadOrientationConverter
{
    private double _calibratedYaw;
    private double _calibratedPitch;
    private double _calibratedRoll;

    /// <summary>
    /// Sets the current raw orientation as the zero reference.
    /// After calibration, the head's current position becomes (0, 0, 0).
    /// </summary>
    public void Calibrate(double rawYawDeg, double rawPitchDeg, double rawRollDeg)
    {
        _calibratedYaw = rawYawDeg;
        _calibratedPitch = rawPitchDeg;
        _calibratedRoll = rawRollDeg;
    }

    /// <summary>
    /// Resets calibration to zero (factory default).
    /// </summary>
    public void ResetCalibration()
    {
        _calibratedYaw = 0;
        _calibratedPitch = 0;
        _calibratedRoll = 0;
    }

    /// <summary>
    /// Converts raw head orientation to HRTF azimuth and elevation,
    /// applying calibration and normalization.
    /// Returns (azimuthDeg, elevationDeg) in the HRTF coordinate system.
    /// </summary>
    public (double AzimuthDeg, double ElevationDeg) Convert(
        double rawYawDeg, double rawPitchDeg, double rawRollDeg)
    {
        // Relative orientation after calibration
        var relativeYaw = rawYawDeg - _calibratedYaw;
        var relativePitch = rawPitchDeg - _calibratedPitch;

        // Yaw maps directly to azimuth (positive yaw = left = positive azimuth)
        var azimuth = relativeYaw;

        // Pitch maps to elevation (positive pitch = up = positive elevation)
        var elevation = relativePitch;

        // Normalize azimuth to -180°..+180°
        azimuth = NormalizeAzimuth(azimuth);

        // Clamp elevation to HRTF-supported range
        elevation = Math.Clamp(elevation, -90.0, 90.0);

        return (azimuth, elevation);
    }

    /// <summary>
    /// Normalizes an angle in degrees to the range -180°..+180°.
    /// </summary>
    public static double NormalizeAzimuth(double degrees)
    {
        degrees %= 360.0;
        if (degrees > 180.0) degrees -= 360.0;
        else if (degrees < -180.0) degrees += 360.0;
        return degrees;
    }
}
