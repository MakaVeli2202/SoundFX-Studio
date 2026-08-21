using SoundFXStudio.Models;

namespace SoundFXStudio.Services.Hrtf;

/// <summary>
/// Loads HRTF profiles from SOFA (Spatially Oriented Format for Acoustics) files.
/// The implementation is responsible for parsing the SOFA/NetCDF format and
/// producing valid HrtfProfile instances. UI and ViewModel code never touch
/// the underlying NetCDF library directly.
/// </summary>
public interface ISofaHrtfLoader
{
    /// <summary>
    /// Loads an HRTF profile from the specified SOFA file path.
    /// Returns a structured result containing the profile on success
    /// or error information on failure.
    /// </summary>
    SofaHrtfLoadResult Load(string filePath);
}
