namespace SoundFXStudio.Services.DSP;

public sealed class RobotEffect : IAudioEffect
{
    private double _phase;

    public RobotEffect(int sampleRate = 48000)
    {
        SampleRate = sampleRate;
    }

    public string Name => "Robot";

    public bool IsEnabled { get; set; }

    public int SampleRate { get; set; }

    public double FrequencyHz { get; set; } = 30.0;

    public double Depth { get; set; } = 0.5;

    public void Process(Span<float> buffer)
    {
        var step = 2.0 * Math.PI * FrequencyHz / SampleRate;
        var baseGain = 1.0 - Depth * 0.5;

        for (int i = 0; i < buffer.Length; i++)
        {
            var mod = baseGain + Depth * 0.5 * Math.Sin(_phase);
            buffer[i] = (float)(buffer[i] * mod);
            _phase += step;
            if (_phase >= 2.0 * Math.PI)
            {
                _phase -= 2.0 * Math.PI;
            }
        }
    }

    public void Reset() => _phase = 0.0;
}
