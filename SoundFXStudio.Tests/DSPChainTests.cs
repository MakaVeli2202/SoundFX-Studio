using NAudio.Wave;
using SoundFXStudio.Services.DSP;
using Xunit;

namespace SoundFXStudio.Tests;

public class DSPChainTests
{
    [Fact]
    public void NoiseGate_QuietSignal_IsAttenuated()
    {
        var gate = new NoiseGateEffect { ThresholdDb = -30, AttackMs = 2, ReleaseMs = 50 };
        var data = Sine(0.001f, 48000);

        gate.Process(data);

        var tail = data.AsSpan(24000);
        Assert.True(MeanAbs(tail) < 1e-3, $"gate did not attenuate, mean={MeanAbs(tail)}");
    }

    [Fact]
    public void NoiseGate_LoudSignal_PassesThrough()
    {
        var gate = new NoiseGateEffect { ThresholdDb = -30, AttackMs = 2, ReleaseMs = 50 };
        var data = Sine(0.5f, 48000);

        gate.Process(data);

        var tail = data.AsSpan(24000);
        Assert.True(MeanAbs(tail) > 0.1, $"gate attenuated loud signal, mean={MeanAbs(tail)}");
    }

    [Fact]
    public void Limiter_ClampsPeaksToThreshold()
    {
        var limiter = new LimiterEffect { Threshold = 0.95, ReleaseMs = 50 };
        var data = Sine(1.5f, 48000);

        limiter.Process(data);

        var tail = data.AsSpan(24000);
        Assert.True(MaxAbs(tail) <= 0.9505, $"limiter did not clamp, max={MaxAbs(tail)}");
    }

    [Fact]
    public void Limiter_SmallSignal_PassesUnchanged()
    {
        var limiter = new LimiterEffect { Threshold = 0.95, ReleaseMs = 50 };
        var data = Sine(0.1f, 48000);

        limiter.Process(data);

        var tail = data.AsSpan(24000);
        Assert.InRange(MaxAbs(tail), 0.099, 0.101);
    }

    [Fact]
    public void Chain_ProcessesInOrderAndSkipsDisabled()
    {
        var chain = new DSPChain();
        var a = new CountingEffect("A");
        var b = new CountingEffect("B");
        var c = new CountingEffect("C") { IsEnabled = false };
        chain.Add(a);
        chain.Add(b);
        chain.Add(c);

        var buffer = new float[64];
        chain.Process(buffer);

        Assert.Equal(1, a.ProcessCount);
        Assert.Equal(1, b.ProcessCount);
        Assert.Equal(0, c.ProcessCount);
    }

    [Fact]
    public void Chain_Disabled_SkipsEverything()
    {
        var chain = new DSPChain { IsEnabled = false };
        var a = new CountingEffect("A");
        chain.Add(a);

        chain.Process(new float[64]);

        Assert.Equal(0, a.ProcessCount);
    }

    [Fact]
    public void Chain_Reset_ResetsAllEffects()
    {
        var chain = new DSPChain();
        var a = new CountingEffect("A");
        chain.Add(a);

        chain.Reset();

        Assert.Equal(1, a.ResetCount);
    }

    [Fact]
    public void Chain_Get_ReturnsEffectByType()
    {
        var chain = new DSPChain();
        var a = new CountingEffect("A");
        chain.Add(a);

        Assert.Same(a, chain.Get<CountingEffect>());
    }

    [Fact]
    public void EffectSampleProvider_AppliesChainToSource()
    {
        var source = new FixedSource(new[] { 0.25f, -0.5f, 0.75f, -1.0f, 0.5f });
        var chain = new DSPChain();
        chain.Add(new AddOffsetEffect(0.1f));
        var provider = new EffectSampleProvider(source, chain);

        var output = new float[5];
        var read = provider.Read(output, 0, output.Length);

        Assert.Equal(5, read);
        Assert.Equal(new[] { 0.35f, -0.4f, 0.85f, -0.9f, 0.6f }, output);
    }

    [Fact]
    public void FormantShift_FactorOne_PassesThrough()
    {
        var source = new FixedSource(new[] { 0.25f, -0.5f, 0.75f, -1.0f, 0.5f });
        var formant = new FormantShiftSampleProvider(source);

        var output = new float[5];
        var read = formant.Read(output, 0, output.Length);

        Assert.Equal(5, read);
        Assert.Equal(new[] { 0.25f, -0.5f, 0.75f, -1.0f, 0.5f }, output);
    }

    [Fact]
    public void VoiceTransform_Neutral_PassesThrough()
    {
        var source = new FixedSource(new[] { 0.25f, -0.5f, 0.75f, -1.0f, 0.5f });
        var transform = new VoiceTransformSampleProvider(source);

        var output = new float[5];
        var read = transform.Read(output, 0, output.Length);

        Assert.Equal(5, read);
        Assert.Equal(new[] { 0.25f, -0.5f, 0.75f, -1.0f, 0.5f }, output);
    }

    [Fact]
    public void VoiceTransform_ChangedFactors_ProducesNonSilentOutput()
    {
        var source = new LoopingSource(Sine(0.5f, 4096));
        var transform = new VoiceTransformSampleProvider(source)
        {
            PitchFactor = SemitonesToFactor(4f),
            FormantFactor = 1.2f
        };

        var output = new float[8192];
        float max = 0;
        for (int i = 0; i < 8; i++)
        {
            transform.Read(output, 0, output.Length);
            for (int j = 0; j < output.Length; j++)
            {
                max = Math.Max(max, Math.Abs(output[j]));
            }
        }

        Assert.True(max > 1e-3f, $"expected audible output, got {max}");
        Assert.All(output, v => Assert.True(float.IsFinite(v), "output must be finite"));
    }

    private static float SemitonesToFactor(float semitones)
    {
        return MathF.Pow(2f, semitones / 12f);
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

    [Fact]
    public void Chain_ExceptionInEffect_DoesNotKillSubsequentEffects()
    {
        var chain = new DSPChain();
        var a = new AddOffsetEffect(0.1f);
        var b = new ThrowingEffect();
        var c = new AddOffsetEffect(0.2f);
        chain.Add(a);
        chain.Add(b);
        chain.Add(c);

        var buffer = new float[] { 0.5f, -0.5f };
        chain.Process(buffer);

        Assert.Equal(0.8f, buffer[0]);
        Assert.Equal(-0.2f, buffer[1]);
    }

    private sealed class ThrowingEffect : IAudioEffect
    {
        public string Name => "throw";
        public bool IsEnabled { get; set; } = true;
        public void Process(Span<float> buffer) => throw new InvalidOperationException("boom");
        public void Reset() { }
    }

    private sealed class CountingEffect : IAudioEffect
    {
        public CountingEffect(string name) => Name = name;

        public string Name { get; }

        public bool IsEnabled { get; set; } = true;

        public int ProcessCount { get; private set; }

        public int ResetCount { get; private set; }

        public void Process(Span<float> buffer) => ProcessCount++;

        public void Reset() => ResetCount++;
    }

    private sealed class AddOffsetEffect : IAudioEffect
    {
        private readonly float _offset;

        public AddOffsetEffect(float offset) => _offset = offset;

        public string Name => "offset";

        public bool IsEnabled { get; set; } = true;

        public void Process(Span<float> buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] += _offset;
            }
        }

        public void Reset()
        {
        }
    }

    private sealed class FixedSource : ISampleProvider
    {
        private readonly float[] _samples;
        private int _index;

        public FixedSource(float[] samples) => _samples = samples;

        public WaveFormat WaveFormat => WaveFormat.CreateIeeeFloatWaveFormat(48000, 1);

        public int Read(float[] buffer, int offset, int count)
        {
            int written = 0;
            while (written < count && _index < _samples.Length)
            {
                buffer[offset + written] = _samples[_index++];
                written++;
            }
            return written;
        }
    }

    private sealed class LoopingSource : ISampleProvider
    {
        private readonly float[] _samples;
        private int _index;

        public LoopingSource(float[] samples) => _samples = samples;

        public WaveFormat WaveFormat => WaveFormat.CreateIeeeFloatWaveFormat(48000, 1);

        public int Read(float[] buffer, int offset, int count)
        {
            for (int i = 0; i < count; i++)
            {
                buffer[offset + i] = _samples[_index++ % _samples.Length];
            }
            return count;
        }
    }
}
