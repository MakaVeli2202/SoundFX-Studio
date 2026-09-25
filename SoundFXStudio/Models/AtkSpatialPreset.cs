using System.Collections.Generic;

namespace SoundFXStudio.Models;

/// <summary>
/// Lossless representation of an ArtTuneKit VST chunk found in ArtTuneDB V5+ tune
/// files. The chunk (base64 "ChunkData") encodes a proprietary spatial-engine
/// program state: an ASCII header, a version token and a list of float values.
///
/// The float layout is NOT the 59 JSFX sliders of atk_spatial_engine.jsfx and the
/// mapping to those sliders cannot be proven without the private
/// atk_spatial_engine_bravo DLL as reference. The raw values are therefore kept
/// verbatim so a faithful mapping can be applied later without data loss.
/// </summary>
public sealed class AtkSpatialPreset
{
    /// <summary>Engine identifier from the chunk header, e.g. "ATK-SE-16CH-V1-4".</summary>
    public string Magic { get; set; } = string.Empty;

    /// <summary>Program version token following the header (e.g. "1").</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>Plugin path embedded in the VSTPlugin: line that carries this chunk.</summary>
    public string PluginLibrary { get; set; } = string.Empty;

    /// <summary>Raw float program state, exactly as serialized by the engine.</summary>
    public List<double> RawValues { get; set; } = new();

    /// <summary>Channel count convention implied by the header magics this tool recognises.</summary>
    public int ChannelCount { get; set; }

    public AtkSpatialPreset Clone()
    {
        return new AtkSpatialPreset
        {
            Magic = Magic,
            Version = Version,
            PluginLibrary = PluginLibrary,
            ChannelCount = ChannelCount,
            RawValues = new List<double>(RawValues)
        };
    }
}