namespace SoundFXStudio.Services.DSP;

public sealed class LimiterEffect : IAudioEffect
{
    private double _gain = 1.0;

    public LimiterEffect(int sampleRate = 48000)
    {
        SampleRate = sampleRate;
    }

    public string Name => "Limiter";

    public bool IsEnabled { get; set; } = true;

    public int SampleRate { get; set; }

    public double Threshold { get; set; } = 0.95;

    public double ReleaseMs { get; set; } = 100.0;

    public void Process(Span<float> buffer)
    {
        var releaseCoeff = 1.0 - Math.Exp(-1.0 / Math.Max(1.0, SampleRate * ReleaseMs / 1000.0));

        for (int i = 0; i < buffer.Length; i++)
        {
            var abs = Math.Abs((double)buffer[i]);

            if (abs > Threshold)
            {
                _gain = Math.Min(_gain, Threshold / abs);
            }
            else
            {
                _gain += (1.0 - _gain) * releaseCoeff;
            }

            buffer[i] = (float)(buffer[i] * _gain);
        }
    }

    public void Reset() => _gain = 1.0;
}
