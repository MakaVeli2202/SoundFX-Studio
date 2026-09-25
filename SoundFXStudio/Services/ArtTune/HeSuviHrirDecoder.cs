using System.Buffers.Binary;
using System.IO;
using SoundFXStudio.Models;

namespace SoundFXStudio.Services.ArtTune;

/// <summary>
/// Decodes the 14-channel EAC default HRIR WAV used by the ArtTuneDB repository
/// into a SoundFX HrtfProfile.
///
/// Channel plan (HeSuVi EAC Export):
///   0 FL-L   1 FL-R   2 SL-L   3 SL-R   4 BL-L   5 BL-R
///   6 FC-L   7 FR-R   8 FR-L   9 SR-R  10 SR-L  11 BR-R  12 BR-L  13 FC-R
///
/// Positive azimuth = left (matches the application convention, no negation applied).
/// </summary>
public static class HeSuviHrirDecoder
{
    private const int ExpectedChannels = 14;
    private const int MaxTaps = 512;

    // (leftChannel, rightChannel, azimuthDeg)
    private static readonly (int Left, int Right, double Azimuth)[] ChannelPlan =
    {
        (0, 1, 30),     // Front Left
        (6, 13, 0),     // Front Center
        (8, 7, -30),    // Front Right
        (2, 3, 90),     // Side Left
        (10, 9, -90),   // Side Right
        (4, 5, 150),    // Back Left
        (12, 11, -150)  // Back Right
    };

    /// <summary>
    /// Decodes raw WAV bytes into an HrtfProfile. Returns null when the file is not
    /// a valid 14-channel floating point WAV.
    /// </summary>
    public static HrtfProfile? DecodeEacDefault(byte[] wavData)
    {
        if (wavData is null || wavData.Length < 44)
            return null;

        var format = ReadWaveFormat(wavData);
        if (format is null)
            return null;

        int channels = format.Channels;
        int sampleRate = format.SampleRate;
        if (channels != ExpectedChannels || (format.FormatTag != 3 && format.FormatTag != 1))
            return null;

        int bytesPerSample = format.BitsPerSample / 8;
        if (bytesPerSample < 1)
            return null;

        var headSizes = wavData.Length - format.DataOffset;
        int frameCount = headSizes / (bytesPerSample * channels);
        if (frameCount < 4)
            return null;

        // De-interleave into per-channel float buffers
        var channelData = new float[channels][];
        for (int c = 0; c < channels; c++)
            channelData[c] = new float[frameCount];

        int offset = format.DataOffset;
        for (int frame = 0; frame < frameCount; frame++)
        {
            for (int c = 0; c < channels; c++)
            {
                channelData[c][frame] = format.FormatTag == 3
                    ? ReadFloat(wavData, offset)
                    : ReadPcm16(wavData, offset);
                offset += bytesPerSample;
            }
        }

        var entries = new List<HrtfEntry>(ChannelPlan.Length);
        float globalPeak = 0f;

        // First pass: extract raw windows and find the global peak across all channels
        // so relative inter-channel levels are preserved (per-window normalization
        // would over-amplify heavily damped channels).
        var windows = new List<(float[] Left, float[] Right, double Azimuth)>();
        foreach (var (leftChannel, rightChannel, azimuth) in ChannelPlan)
        {
            if (leftChannel < 0 || leftChannel >= channels || rightChannel < 0 || rightChannel >= channels)
                continue;

            var left = ExtractWindow(channelData[leftChannel]);
            var right = ExtractWindow(channelData[rightChannel]);
            if (left is null || right is null)
                continue;

            windows.Add((left, right, azimuth));
            globalPeak = Math.Max(globalPeak, Math.Max(PeakAbs(left), PeakAbs(right)));
        }

        if (windows.Count == 0 || globalPeak <= 0f)
            return null;

        var irLength = windows[0].Left.Length;
        foreach (var (left, right, azimuth) in windows)
        {
            entries.Add(new HrtfEntry
            {
                AzimuthDeg = azimuth,
                ElevationDeg = 0,
                LeftEarResponse = ScaleToGlobal(left, globalPeak),
                RightEarResponse = ScaleToGlobal(right, globalPeak)
            });
        }

        return new HrtfProfile
        {
            Id = ArtTuneLibraryService.HrtfProfileId,
            Name = "EAC Default (ArtTune)",
            Manufacturer = "EAC / ArtTuneDB",
            Description = "Equale Audio Capture default 14-channel head-related impulse response from the ArtTuneDB repository.",
            SampleRate = sampleRate,
            IrLength = irLength,
            Entries = entries.ToArray(),
            DataSource = "EAC Default (14ch Float32)",
            License = "Open source — see ArtIsWar/ArtTuneDB"
        };
    }

    private sealed class WaveFormat
    {
        public ushort FormatTag;
        public ushort Channels;
        public int SampleRate;
        public ushort BitsPerSample;
        public int DataOffset;
    }

    private static WaveFormat? ReadWaveFormat(byte[] data)
    {
        if (data.Length < 12)
            return null;
        if (data[0] != (byte)'R' || data[1] != (byte)'I' || data[2] != (byte)'F' || data[3] != (byte)'F')
            return null;
        if (data[8] != (byte)'W' || data[9] != (byte)'A' || data[10] != (byte)'V' || data[11] != (byte)'E')
            return null;

        WaveFormat? format = null;
        int dataOffset = -1;

        int cursor = 12;
        while (cursor + 8 <= data.Length)
        {
            var chunkId = (char)data[cursor] + "" + (char)data[cursor + 1] + (char)data[cursor + 2] + (char)data[cursor + 3];
            int chunkSize = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(cursor + 4, 4));
            int chunkStart = cursor + 8;

            if (chunkId == "fmt ")
            {
                if (chunkStart + 14 > data.Length)
                    return null;
                format = new WaveFormat
                {
                    FormatTag = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(chunkStart, 2)),
                    Channels = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(chunkStart + 2, 2)),
                    SampleRate = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(chunkStart + 4, 4)),
                    BitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(chunkStart + 12, 2))
                };
            }
            else if (chunkId == "data")
            {
                dataOffset = chunkStart;
                if (format is null)
                    return null;
                break;
            }

            if (chunkSize <= 0)
            {
                cursor += 8;
            }
            else
            {
                cursor = chunkStart + chunkSize + (chunkSize % 2);
            }
        }

        if (format is null || dataOffset < 0)
            return null;

        format.DataOffset = dataOffset;
        return format;
    }

    private static float ReadFloat(byte[] data, int offset)
    {
        int bits = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4));
        return BitConverter.Int32BitsToSingle(bits);
    }

    private static float ReadPcm16(byte[] data, int offset)
    {
        return BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset, 2)) / 32768f;
    }

    /// <summary>
    /// Windows the impulse around its peak. Returns null when the source buffer
    /// contains only silence. Normalization is deferred to a global scale.
    /// </summary>
    private static float[]? ExtractWindow(float[] source)
    {
        var inputLength = source.Length;
        int peakIndex = 0;
        float peakValue = 0f;
        for (int i = 0; i < inputLength; i++)
        {
            var abs = Math.Abs(source[i]);
            if (abs > peakValue)
            {
                peakValue = abs;
                peakIndex = i;
            }
        }

        if (peakValue <= 0f)
            return null;

        int half = MaxTaps / 2;
        int start = Math.Max(0, peakIndex - half);
        int end = Math.Min(inputLength, start + MaxTaps);
        start = Math.Max(0, end - MaxTaps);

        var windowed = new float[end - start];
        for (int i = start; i < end; i++)
            windowed[i - start] = source[i];

        return windowed;
    }

    private static float PeakAbs(float[] samples)
    {
        float peak = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            var abs = Math.Abs(samples[i]);
            if (abs > peak) peak = abs;
        }
        return peak;
    }

    /// <summary>
    /// Scales a window relative to a global peak so channel differences are kept.
    /// </summary>
    private static float[] ScaleToGlobal(float[] samples, float globalPeak)
    {
        var scaled = new float[samples.Length];
        for (int i = 0; i < samples.Length; i++)
            scaled[i] = samples[i] / globalPeak;
        return scaled;
    }
}