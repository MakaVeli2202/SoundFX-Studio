using NAudio.Wave;

namespace SoundFXStudio.Services.DSP;

public sealed class DownmixToMonoSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private float[]? _mixBuffer;

    public DownmixToMonoSampleProvider(ISampleProvider source)
    {
        _source = source;
    }

    public WaveFormat WaveFormat => WaveFormat.CreateIeeeFloatWaveFormat(_source.WaveFormat.SampleRate, 1);

    public int Read(float[] buffer, int offset, int count)
    {
        var sourceChannels = _source.WaveFormat.Channels;
        var requiredSamples = count * sourceChannels;
        if (_mixBuffer is null || _mixBuffer.Length < requiredSamples)
        {
            _mixBuffer = new float[requiredSamples];
        }

        var read = _source.Read(_mixBuffer, 0, requiredSamples);
        var frames = read / sourceChannels;

        for (var i = 0; i < frames; i++)
        {
            var sum = 0f;
            for (var c = 0; c < sourceChannels; c++)
            {
                sum += _mixBuffer[i * sourceChannels + c];
            }
            buffer[offset + i] = sum / sourceChannels;
        }

        return frames;
    }
}
