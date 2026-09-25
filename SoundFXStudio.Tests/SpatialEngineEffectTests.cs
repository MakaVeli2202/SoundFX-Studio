using SoundFXStudio.Services.DSP;
using Xunit;

namespace SoundFXStudio.Tests;

public class SpatialEngineEffectTests
{
    private const int SampleRate = 48000;

    [Fact]
    public void Stereo_NeutralSettings_ReconstructsInput()
    {
        var engine = new SpatialEngineEffect(SampleRate)
        {
            ChannelCount = 2,
            LowAttack = 0, LowSustain = 0,
            MidAttack = 0, MidSustain = 0,
            HighAttack = 0, HighSustain = 0,
            SurroundLowAttackStr = 0, SurroundLowSustainStr = 0,
            SurroundMidAttackStr = 0, SurroundMidSustainStr = 0,
            SurroundHighAttackStr = 0, SurroundHighSustainStr = 0,
            FrontLowAttackStr = 0, FrontLowSustainStr = 0,
            FrontMidAttackStr = 0, FrontMidSustainStr = 0,
            FrontHighAttackStr = 0, FrontHighSustainStr = 0,
            AeqLowGainDb = 0,
            AeqHighGainDb = 0,
            DryWetMix = 1,
        };
        var data = Interleaved(new[] { Sine(0.5f), Sine(0.25f, 330f) }, 48000);

        engine.Process(data);

        var l = data.Chunk(2).Skip(24000).Select(f => Math.Abs(f[0])).Max();
        var r = data.Chunk(2).Skip(24000).Select(f => Math.Abs(f[1])).Max();
        Assert.InRange(l, 0.45, 0.55);
        Assert.InRange(r, 0.20, 0.30);
    }

    [Fact]
    public void Mode8Channel_LfeChannel_PassesUnchanged()
    {
        var engine = new SpatialEngineEffect(SampleRate)
        {
            ChannelCount = 8,
            LowAttack = 0, LowSustain = 0,
            MidAttack = 0, MidSustain = 0,
            HighAttack = 0, HighSustain = 0,
            SurroundLowAttackStr = 0, SurroundLowSustainStr = 0,
            SurroundMidAttackStr = 0, SurroundMidSustainStr = 0,
            SurroundHighAttackStr = 0, SurroundHighSustainStr = 0,
            FrontLowAttackStr = 0, FrontLowSustainStr = 0,
            FrontMidAttackStr = 0, FrontMidSustainStr = 0,
            FrontHighAttackStr = 0, FrontHighSustainStr = 0,
            AeqLowGainDb = 0,
            AeqHighGainDb = 0,
            DryWetMix = 1,
        };

        var left = Sine(0.5f);
        var lfeOffset = 7;
        var data = new float[8 * 48000];
        for (int i = 0; i < 48000; i++)
        {
            data[i * 8 + 0] = left[i];
            data[i * 8 + lfeOffset] = 0.9f;
        }

        engine.Process(data);

        var maxLfe = data.Chunk(8).Select(f => Math.Abs(f[lfeOffset])).Take(48000).Max();
        Assert.InRange(maxLfe, 0.89, 0.91);
    }

    [Fact]
    public void CenterDuck_ReducesMidButKeepsSide()
    {
        var engine = new SpatialEngineEffect(SampleRate)
        {
            ChannelCount = 6,
            LowAttack = 0, LowSustain = 0,
            MidAttack = 0, MidSustain = 0,
            HighAttack = 0, HighSustain = 0,
            SurroundLowAttackStr = 0, SurroundLowSustainStr = 0,
            SurroundMidAttackStr = 0, SurroundMidSustainStr = 0,
            SurroundHighAttackStr = 0, SurroundHighSustainStr = 0,
            FrontLowAttackStr = 0, FrontLowSustainStr = 0,
            FrontMidAttackStr = 0, FrontMidSustainStr = 0,
            FrontHighAttackStr = 0, FrontHighSustainStr = 0,
            GateDepthDb = -12,
            GateThresholdDb = -60,
            GateAttackMs = 1,
            GateReleaseMs = 20,
            GateKneeDb = 24,
            DryWetMix = 1,
        };

        var tone = Sine(0.3f);
        var center = Sine(0.3f, 440f);
        var data = new float[6 * 48000];
        for (int i = 0; i < 48000; i++)
        {
            data[i * 6 + 0] = tone[i];
            data[i * 6 + 1] = tone[i];
            data[i * 6 + 4] = center[i];
            data[i * 6 + 5] = center[i];
        }

        engine.Process(data);

        var midTail = data.Chunk(6).Skip(24000).Select(f => Math.Abs(f[0])).Average();

        // Input mid amplitude 0.3 * duck ~0.25 => ~0.075 (front L/R ducked).
        Assert.InRange(midTail, 0.02, 0.10);
        Assert.DoesNotContain(float.NaN, data);
    }

    [Fact]
    public void AdaptiveEqBoost_QuietInput_AmplifiesFront()
    {
        var engine = new SpatialEngineEffect(SampleRate)
        {
            ChannelCount = 7,
            LowAttack = 0, LowSustain = 0,
            MidAttack = 0, MidSustain = 0,
            HighAttack = 0, HighSustain = 0,
            SurroundLowAttackStr = 0, SurroundLowSustainStr = 0,
            SurroundMidAttackStr = 0, SurroundMidSustainStr = 0,
            SurroundHighAttackStr = 0, SurroundHighSustainStr = 0,
            FrontLowAttackStr = 0, FrontLowSustainStr = 0,
            FrontMidAttackStr = 0, FrontMidSustainStr = 0,
            FrontHighAttackStr = 0, FrontHighSustainStr = 0,
            AeqLowGainDb = 6,
            AeqLowFreqHz = 800,
            AeqLowQ = 1.0,
            AeqHighGainDb = 0,
            AeqAttackMs = 1,
            AeqReleaseMs = 200,
            AeqRejectThresholdDb = 0,
            DryWetMix = 1,
        };

        var tone = Sine(0.05f, 800f);
        var data = new float[7 * 48000];
        for (int i = 0; i < 48000; i++)
        {
            data[i * 7 + 0] = tone[i];
            data[i * 7 + 1] = tone[i];
        }

        engine.Process(data);

        var maxL = data.Chunk(7).Skip(24000).Select(f => Math.Abs(f[0])).Max();
        // Boost lifts quiet signal toward +6dB-equivalent envelope near slider center (800Hz).
        Assert.InRange(maxL, 0.07, 0.12);
        Assert.DoesNotContain(float.NaN, data);
    }

    [Fact]
    public void Impulses_AllModes_StayFinite()
    {
        foreach (var channels in new[] { 2, 6, 7, 8 })
        {
            var engine = new SpatialEngineEffect(SampleRate)
            {
                ChannelCount = channels,
                SelfSuppress = 0.8,
                CmrLow = 0.5, CmrMid = 0.5, CmrHigh = 0.5,
                DirectionalEmphasis = 0.5,
                LowExpansionRatio = 2, MidExpansionRatio = 2, HighExpansionRatio = 2,
                AeqLowGainDb = 3, AeqHighGainDb = 3,
                DryWetMix = 1,
            };
            var data = new float[channels * 2048];
            foreach (int c in Enumerable.Range(0, channels))
                data[2048 * c + c] = 0.8f;

            engine.Process(data);

            Assert.All(data, f => Assert.True(float.IsFinite(f), $"non-finite at mode {channels}"));
        }
    }

    private static float[] Sine(float amplitude, float freq = 220f, int length = 48000)
    {
        var data = new float[length];
        for (int i = 0; i < length; i++)
            data[i] = (float)(amplitude * Math.Sin(2 * Math.PI * freq * i / SampleRate));
        return data;
    }

    private static float[] Interleaved(IList<float[]> channels, int frames)
    {
        var data = new float[channels.Count * frames];
        for (int i = 0; i < frames; i++)
            for (int c = 0; c < channels.Count; c++)
                data[i * channels.Count + c] = channels[c][i];
        return data;
    }
}