using NAudio.Wave;

namespace SoundFXStudio.Services.DSP;

public sealed class EffectSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly DSPChain _chain;

    public EffectSampleProvider(ISampleProvider source, DSPChain chain)
    {
        _source = source;
        _chain = chain;
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        var read = _source.Read(buffer, offset, count);
        _chain.Process(buffer.AsSpan(offset, read));
        return read;
    }
}
