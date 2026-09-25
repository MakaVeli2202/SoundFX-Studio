using System.Text;
using SoundFXStudio.Models;
using SoundFXStudio.Services.ArtTune;
using Xunit;

namespace SoundFXStudio.Tests;

public class AtkChunkDecoderTests
{
    [Fact]
    public void DecodeLine_LosslessRoundTrip_VersionMagicFloats()
    {
        var sb = new StringBuilder();
        sb.Append(AtkChunkDecoder.Magic16Ch).Append('\0');
        sb.Append("1 ");
        sb.Append("1.000000 -50.000000 12.000000 8.000000 ");
        var b64 = Convert.ToBase64String(Encoding.ASCII.GetBytes(sb.ToString()));

        var line = $"VSTPlugin: Library \"C:\\Program Files\\VSTPlugins\\ArtTuneKit\\atk_spatial_engine_bravo_v2_0_0.dll\" ChunkData \"{b64}\"";

        var preset = AtkChunkDecoder.DecodeLine(line);

        Assert.NotNull(preset);
        Assert.Equal(AtkChunkDecoder.Magic16Ch, preset!.Magic);
        Assert.Equal("1", preset.Version);
        Assert.Equal(4, preset.RawValues.Count);
        Assert.Equal(1.0, preset.RawValues[0]);
        Assert.Equal(-50.0, preset.RawValues[1]);
        Assert.Equal(12.0, preset.RawValues[2]);
        Assert.Equal(8.0, preset.RawValues[3]);
        Assert.Equal(16, preset.ChannelCount);
        Assert.Equal("C:\\Program Files\\VSTPlugins\\ArtTuneKit\\atk_spatial_engine_bravo_v2_0_0.dll", preset.PluginLibrary);
    }

    [Fact]
    public void DecodeLine_RejectsNonChunkLine_ReturnsNull()
    {
        Assert.Null(AtkChunkDecoder.DecodeLine("Filter 1: ON PK Fc 31 Hz Gain 0.0 dB Q 1.41"));
        Assert.Null(AtkChunkDecoder.DecodeLine("# comment line"));
        Assert.Null(AtkChunkDecoder.DecodeLine(""));
        Assert.Null(AtkChunkDecoder.DecodeLine("VSTPlugin: ChunkData \"!!!not-base64!!!\""));
    }

    [Fact]
    public void DecodeLine_InvalidBase64_ReturnsNull()
    {
        Assert.Null(AtkChunkDecoder.DecodeLine("VSTPlugin: Library \"x.dll\" ChunkData \"GGGGGG===invalid\""));
    }

    [Fact]
    public void DecodeFirst_FirstChunkLineAcrossCommentLines_IsFound()
    {
        var text = "### TUNE: Balanced\n### OUTPUT: 16ch\n" +
                   "VSTPlugin: Library \"C:\\atk.dll\" ChunkData \"" +
                   Convert.ToBase64String(Encoding.ASCII.GetBytes("ATK-SE-16CH-V1-4\0" + "1 " + "20.0 30.0 40.0")) +
                   "\"\n";

        var preset = AtkChunkDecoder.DecodeFirst(text);

        Assert.NotNull(preset);
        Assert.Equal("ATK-SE-16CH-V1-4", preset!.Magic);
        Assert.Equal(3, preset.RawValues.Count);
        Assert.Equal(20.0, preset.RawValues[0]);
        Assert.Equal(40.0, preset.RawValues[^1]);
    }

    [Fact]
    public void DecodeFirst_NoChunk_ReturnsNull()
    {
        var text = "Preamp: -5.8 dB\nFilter 1: ON PK Fc 31 Hz Gain 0.0 dB Q 1.41\n";
        Assert.Null(AtkChunkDecoder.DecodeFirst(text));
    }

    [Fact]
    public void Clone_IsDeepCopy()
    {
        var a = new AtkSpatialPreset { Magic = "M", Version = "1", RawValues = new List<double> { 1, 2 } };
        var b = a.Clone();
        b.RawValues.Add(3);
        Assert.Equal(2, a.RawValues.Count);
        Assert.Equal(3, b.RawValues.Count);
    }
}