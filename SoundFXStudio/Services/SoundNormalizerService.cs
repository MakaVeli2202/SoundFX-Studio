using NAudio.Wave;
using System.IO;

namespace SoundFXStudio.Services;

public readonly record struct SoundLevels(float Peak, float Rms);

/// <summary>
/// Measures the loudness of an audio file so sounds can be leveled to a consistent volume.
/// </summary>
public sealed class SoundNormalizerService
{
    public SoundLevels Analyze(string filePath)
    {
        using var reader = new AudioFileReader(filePath);

        var sampleRate = reader.WaveFormat.SampleRate > 0 ? reader.WaveFormat.SampleRate : 44100;
        var channels = reader.WaveFormat.Channels > 0 ? reader.WaveFormat.Channels : 2;
        var buffer = new float[sampleRate * channels / 2];

        double sumSquares = 0;
        double peak = 0;
        long total = 0;

        int read;
        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (var i = 0; i < read; i++)
            {
                var sample = Math.Abs((double)buffer[i]);
                if (sample > peak)
                {
                    peak = sample;
                }

                sumSquares += sample * sample;
            }

            total += read;
        }

        var rms = total > 0 ? Math.Sqrt(sumSquares / total) : 0;
        return new SoundLevels((float)peak, (float)rms);
    }

    /// <summary>
    /// Computes the gain that brings a sound's peak and average level to the requested target.
    /// Both bounds are honored (whichever needs less gain wins) so nothing ever exceeds the target peak.
    /// </summary>
    public static float ComputeGain(float peak, float rms, float targetPercent)
    {
        var targetPeak = Math.Clamp(targetPercent, 1f, 100f) / 100f;
        var targetRms = targetPeak * 0.5f;

        var peakGuard = Math.Max(peak, 0.0001f);
        var rmsGuard = Math.Max(rms, 0.0001f);

        var gain = Math.Min(targetPeak / peakGuard, targetRms / rmsGuard);
        return Math.Clamp(gain, 0.05f, 100f);
    }
}
