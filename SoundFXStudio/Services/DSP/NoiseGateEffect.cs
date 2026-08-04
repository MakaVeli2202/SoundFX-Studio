namespace SoundFXStudio.Services.DSP;

public sealed class NoiseGateEffect : IAudioEffect
{
    private double _envelope = 1e-4;
    private double _gain = 1.0;

    public NoiseGateEffect(int sampleRate = 48000)
    {
        SampleRate = sampleRate;
    }

    public string Name => "Noise Gate";

    public bool IsEnabled { get; set; } = true;

    public int SampleRate { get; set; }

    public double ThresholdDb { get; set; } = -45.0;

    public double AttackMs { get; set; } = 2.0;

    public double ReleaseMs { get; set; } = 120.0;

    public void Process(Span<float> buffer)
    {
        var threshold = Math.Pow(10.0, ThresholdDb / 20.0);
        var attackCoeff = 1.0 - Math.Exp(-1.0 / Math.Max(1.0, SampleRate * AttackMs / 1000.0));
        var releaseCoeff = 1.0 - Math.Exp(-1.0 / Math.Max(1.0, SampleRate * ReleaseMs / 1000.0));

        for (int i = 0; i < buffer.Length; i++)
        {
            var level = Math.Abs((double)buffer[i]);
            _envelope += (level - _envelope) * (level > _envelope ? attackCoeff : releaseCoeff);

            var target = _envelope > threshold ? 1.0 : 0.0;
            _gain += (target - _gain) * (target > _gain ? attackCoeff : releaseCoeff);

            buffer[i] = (float)(buffer[i] * _gain);
        }
    }

    public void Reset()
    {
        _envelope = 1e-4;
        _gain = 1.0;
    }
}
