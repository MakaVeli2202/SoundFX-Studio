namespace SoundFXStudio.Services.DSP;

public sealed class DistortionEffect : IAudioEffect
{
    public DistortionEffect(int sampleRate = 48000)
    {
        SampleRate = sampleRate;
    }

    public string Name => "Distortion";

    public bool IsEnabled { get; set; }

    public int SampleRate { get; set; }

    public double Drive { get; set; } = 4.0;

    public double PostGain { get; set; } = 1.0;

    public void Process(Span<float> buffer)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            var shaped = Math.Tanh((double)buffer[i] * Drive);
            buffer[i] = (float)(shaped * PostGain);
        }
    }

    public void Reset()
    {
    }
}
