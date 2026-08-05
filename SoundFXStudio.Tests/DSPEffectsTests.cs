using SoundFXStudio.Services.DSP;
using Xunit;

namespace SoundFXStudio.Tests;

public class DSPEffectsTests
{
    [Fact]
    public void Compressor_ReducesPeakOfLoudSignal()
    {
        var compressor = new CompressorEffect(48000) { ThresholdDb = -20, Ratio = 4, AttackMs = 2, ReleaseMs = 80 };
        var data = Sine(1.0f, 48000);

        compressor.Process(data);

        var tail = data.AsSpan(24000);
        Assert.True(MaxAbs(tail) < 0.4, $"compressor did not reduce peak, max={MaxAbs(tail)}");
    }

    [Fact]
    public void Compressor_QuietSignal_PassesUnchanged()
    {
        var compressor = new CompressorEffect(48000) { ThresholdDb = -20, Ratio = 4, AttackMs = 2, ReleaseMs = 80 };
        var data = Sine(0.01f, 48000);

        compressor.Process(data);

        var tail = data.AsSpan(24000);
        Assert.InRange(MaxAbs(tail), 0.009, 0.011);
    }

    [Fact]
    public void Distortion_ShapesSignal()
    {
        var distortion = new DistortionEffect(48000) { Drive = 4, PostGain = 1 };
        var data = Sine(0.2f, 48000);

        distortion.Process(data);

        var tail = data.AsSpan(24000);
        Assert.True(MaxAbs(tail) > 0.5, $"distortion did not drive signal, max={MaxAbs(tail)}");
        Assert.True(MaxAbs(tail) <= 1.0);
    }

    [Fact]
    public void Robot_ModulatesSignal()
    {
        var robot = new RobotEffect(48000) { FrequencyHz = 30, Depth = 0.5 };
        var data = Sine(0.5f, 48000);

        robot.Process(data);

        var tail = data.AsSpan(24000);
        Assert.True(MaxAbs(tail) > 0.2 && MaxAbs(tail) <= 0.55, $"robot modulation out of range, max={MaxAbs(tail)}");
    }

    [Fact]
    public void Reverb_Impulse_ProducesTail()
    {
        var reverb = new ReverbEffect(48000) { Mix = 0.5, RoomSize = 0.8 };
        var data = new float[48000];
        data[0] = 1.0f;

        reverb.Process(data);

        var echoRegion = data.AsSpan(500, 12000);
        Assert.True(MaxAbs(echoRegion) > 1e-3, "reverb produced no tail");
        Assert.DoesNotContain(float.NaN, data);
    }

    [Fact]
    public void Chorus_OutputIsFiniteAndPresent()
    {
        var chorus = new ChorusEffect(48000) { Mix = 0.4, DepthMs = 4, RateHz = 0.8, BaseDelayMs = 20 };
        var data = Sine(0.5f, 48000);

        chorus.Process(data);

        var tail = data.AsSpan(24000);
        Assert.DoesNotContain(float.NaN, data);
        Assert.True(MeanAbs(tail) > 0.05, "chorus produced no output");
        Assert.True(MaxAbs(tail) <= 0.6, $"chorus too loud, max={MaxAbs(tail)}");
    }

    private static float[] Sine(float amplitude, int length, float freq = 220f, int sampleRate = 48000)
    {
        var data = new float[length];
        for (int i = 0; i < length; i++)
        {
            data[i] = (float)(amplitude * Math.Sin(2 * Math.PI * freq * i / sampleRate));
        }
        return data;
    }

    private static double MeanAbs(Span<float> data)
    {
        double sum = 0;
        for (int i = 0; i < data.Length; i++)
        {
            sum += Math.Abs(data[i]);
        }
        return sum / data.Length;
    }

    private static float MaxAbs(Span<float> data)
    {
        float max = 0;
        for (int i = 0; i < data.Length; i++)
        {
            max = Math.Max(max, Math.Abs(data[i]));
        }
        return max;
    }
}
