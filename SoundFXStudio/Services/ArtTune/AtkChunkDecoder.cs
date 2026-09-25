using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SoundFXStudio.Models;

namespace SoundFXStudio.Services.ArtTune;

/// <summary>
/// Decodes ArtTuneKit VST plugin chunks embedded in ArtTuneDB tune files.
///
/// V5+ tune files replace the classic EqualizerAPO filter list with a single
/// line of the form:
///   VSTPlugin: Library "..." ChunkData "<base64>"
///
/// The chunk binary layout is:
///   <ASCII magic, NUL terminated> <version token> <space separated floats>
/// e.g. "ATK-SE-16CH-V1-4" \0 "1 " "1.000000 -50.000000 12.000000 ..."
///
/// The number of floats observed across the V5 16ch variants is 300. This is
/// the engine's internal program state and does not correspond 1:1 to the 59
/// public sliders of the open-source atk_spatial_engine.jsfx; the mapping is
/// intentionally left uninterpreted (see AtkSpatialPreset).
/// </summary>
public static class AtkChunkDecoder
{
    /// <summary>Magics observed in the ArtTuneDB V5 library family.</summary>
    public const string Magic16Ch = "ATK-SE-16CH-V1-4";
    /// <summary>Head-tracked decode used by earlier (V3/V4) generations.</summary>
    public const string Magic74 = "ATK-SE-74-V1-4";
    /// <summary>Stereo decode used by earlier (V3/V4) generations.</summary>
    public const string MagicStereo = "ATK-SE-ST-V1-5";

    private static readonly Regex ChunkLineRegex = new(
        @"^\s*VSTPlugin:\s*(?:Library\s+""([^""]+)""\s+)?ChunkData\s+""([A-Za-z0-9+/=]+)""\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Extracts and decodes the first VSTPlugin ChunkData line found in a tune
    /// file. Returns null when the file carries no decodable VST chunk.
    /// </summary>
    public static AtkSpatialPreset? DecodeFirst(string tuneText)
    {
        foreach (var rawLine in tuneText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var preset = DecodeLine(rawLine);
            if (preset is not null)
                return preset;
        }
        return null;
    }

    /// <summary>Decodes a single VSTPlugin: line. Returns null when it is not a valid chunk line.</summary>
    public static AtkSpatialPreset? DecodeLine(string line)
    {
        var match = ChunkLineRegex.Match(line);
        if (!match.Success)
            return null;

        var plugin = match.Groups[1].Value;
        var base64 = match.Groups[2].Value;

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            return null;
        }

        var text = Encoding.ASCII.GetString(bytes);
        var nulIndex = text.IndexOf('\0');
        if (nulIndex < 0)
            return null;

        var magic = text.Substring(0, nulIndex).Trim();
        var valuesText = text.Substring(nulIndex + 1);

        var tokens = valuesText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
            return null;

        var preset = new AtkSpatialPreset
        {
            Magic = magic,
            Version = tokens[0],
            PluginLibrary = plugin,
            ChannelCount = magic.Contains("16CH", StringComparison.OrdinalIgnoreCase) ? 16 : 0
        };

        for (var i = 1; i < tokens.Length; i++)
        {
            if (double.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                preset.RawValues.Add(value);
        }

        return preset;
    }
}