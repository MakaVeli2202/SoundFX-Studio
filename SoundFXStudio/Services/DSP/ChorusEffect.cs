namespace SoundFXStudio.Services.DSP;

public sealed class ChorusEffect : IAudioEffect
{
    private static readonly double[] VoicePhaseOffsets = { 0.0, Math.PI * 2.0 / 3.0, Math.PI * 4.0 / 3.0 };
    private static readonly double[] VoiceDelayScale = { 1.0, 1.35, 0.7 };

    private float[]? _buffer;
    private int _bufferLength;
    private int _writeIndex;
    private double[] _voicePhase;

    public ChorusEffect(int sampleRate = 48000)
    {
        SampleRate = sampleRate;
        _voicePhase = new double[VoicePhaseOffsets.Length];
    }

    public string Name => "Chorus";

    public bool IsEnabled { get; set; }

    public int SampleRate { get; set; }

    public double Mix { get; set; } = 0.4;

    public double DepthMs { get; set; } = 4.0;

    public double RateHz { get; set; } = 0.8;

    public double BaseDelayMs { get; set; } = 20.0;

    public void Process(Span<float> buffer)
    {
        EnsureBuffer();

        var baseDelay = BaseDelayMs / 1000.0 * SampleRate;
        var depth = DepthMs / 1000.0 * SampleRate;
        var step = 2.0 * Math.PI * RateHz / SampleRate;

        for (int i = 0; i < buffer.Length; i++)
        {
            var input = (double)buffer[i];
            _buffer![_writeIndex] = (float)input;

            var chorus = 0.0;
            for (int v = 0; v < _voicePhase.Length; v++)
            {
                _voicePhase[v] += step * VoiceDelayScale[v];
                if (_voicePhase[v] >= 2.0 * Math.PI)
                {
                    _voicePhase[v] -= 2.0 * Math.PI;
                }

                var modDepth = depth * (0.5 + 0.5 * Math.Sin(_voicePhase[v]));
                var delay = baseDelay * VoiceDelayScale[v] + modDepth;
                var readPos = _writeIndex - delay;
                if (readPos < 0)
                {
                    readPos += _bufferLength;
                }

                var iPos = (int)readPos;
                var frac = readPos - iPos;
                var next = (iPos + 1) % _bufferLength;
                chorus += _buffer[iPos] * (1.0 - frac) + _buffer[next] * frac;
            }

            chorus /= _voicePhase.Length;
            buffer[i] = (float)((1.0 - Mix) * input + Mix * chorus);

            _writeIndex = (_writeIndex + 1) % _bufferLength;
        }
    }

    public void Reset()
    {
        Array.Clear(_voicePhase, 0, _voicePhase.Length);
        _writeIndex = 0;
        if (_buffer is not null)
        {
            Array.Clear(_buffer, 0, _buffer.Length);
        }
    }

    private void EnsureBuffer()
    {
        var needed = (int)Math.Ceiling((BaseDelayMs + DepthMs) / 1000.0 * SampleRate * 2.0);
        if (_buffer is not null && _bufferLength >= needed)
        {
            return;
        }

        _bufferLength = Math.Max(1024, needed);
        _buffer = new float[_bufferLength];
        _writeIndex = 0;
    }
}
