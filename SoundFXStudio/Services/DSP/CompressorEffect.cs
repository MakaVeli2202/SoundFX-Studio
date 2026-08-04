namespace SoundFXStudio.Services.DSP;

public sealed class CompressorEffect : IAudioEffect
{
    private double _envelope = 1e-4;
    private double _gain = 1.0;

    public CompressorEffect(int sampleRate = 48000)
    {
        SampleRate = sampleRate;
    }

    public string Name => "Compressor";

    public bool IsEnabled { get; set; }

    public int SampleRate { get; set; }

    public double ThresholdDb { get; set; } = -20.0;

    public double Ratio { get; set; } = 4.0;

    public double AttackMs { get; set; } = 5.0;

    public double ReleaseMs { get; set; } = 120.0;

    public double MakeUpGainDb { get; set; }

    public void Process(Span<float> buffer)
    {
        var threshold = Math.Pow(10.0, ThresholdDb / 20.0);
        var slope = 1.0 - 1.0 / Math.Max(1.0, Ratio);
        var attackCoeff = 1.0 - Math.Exp(-1.0 / Math.Max(1.0, SampleRate * AttackMs / 1000.0));
        var releaseCoeff = 1.0 - Math.Exp(-1.0 / Math.Max(1.0, SampleRate * ReleaseMs / 1000.0));
        var makeUpGain = Math.Pow(10.0, MakeUpGainDb / 20.0);

        for (int i = 0; i < buffer.Length; i++)
        {
            var level = Math.Abs((double)buffer[i]);
            _envelope += (level - _envelope) * (level > _envelope ? attackCoeff : releaseCoeff);

            double target;
            if (_envelope > threshold)
            {
                var reductionDb = slope * 20.0 * Math.Log10(threshold / _envelope);
                target = Math.Pow(10.0, reductionDb / 20.0);
            }
            else
            {
                target = 1.0;
            }

            _gain += (target - _gain) * (target < _gain ? attackCoeff : releaseCoeff);

            buffer[i] = (float)(buffer[i] * _gain * makeUpGain);
        }
    }

    public void Reset()
    {
        _envelope = 1e-4;
        _gain = 1.0;
    }
}
