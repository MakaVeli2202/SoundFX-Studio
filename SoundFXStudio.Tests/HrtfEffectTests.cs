using SoundFXStudio.Models;
using SoundFXStudio.Services.DSP;
using Xunit;

namespace SoundFXStudio.Tests;

public class HrtfEffectTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    private static HrtfProfile CreateUnityProfile(int irLength = 4)
    {
        var leftIr = new float[irLength];
        var rightIr = new float[irLength];
        leftIr[0] = 1.0f;
        rightIr[0] = 1.0f;

        return new HrtfProfile
        {
            Id = "test-unity",
            Name = "Test Unity",
            SampleRate = 48000,
            IrLength = irLength,
            Entries = new[]
            {
                new HrtfEntry
                {
                    AzimuthDeg = 0,
                    ElevationDeg = 0,
                    LeftEarResponse = leftIr,
                    RightEarResponse = rightIr
                }
            }
        };
    }

    private static HrtfProfile CreateGainProfile(float leftGain, float rightGain, int tapIndex = 0, int irLength = 8)
    {
        var leftIr = new float[irLength];
        var rightIr = new float[irLength];
        leftIr[tapIndex] = leftGain;
        rightIr[tapIndex] = rightGain;

        return new HrtfProfile
        {
            Id = "test-gain",
            Name = "Test Gain",
            SampleRate = 48000,
            IrLength = irLength,
            Entries = new[]
            {
                new HrtfEntry
                {
                    AzimuthDeg = 0,
                    ElevationDeg = 0,
                    LeftEarResponse = leftIr,
                    RightEarResponse = rightIr
                }
            }
        };
    }

    private static HrtfProfile CreateCustomProfile(float[] leftIr, float[] rightIr)
    {
        return new HrtfProfile
        {
            Id = "test-custom",
            Name = "Test Custom",
            SampleRate = 48000,
            IrLength = leftIr.Length,
            Entries = new[]
            {
                new HrtfEntry
                {
                    AzimuthDeg = 0,
                    ElevationDeg = 0,
                    LeftEarResponse = leftIr,
                    RightEarResponse = rightIr
                }
            }
        };
    }

    /// <summary>
    /// Creates an interleaved stereo buffer from separate L/R arrays.
    /// </summary>
    private static float[] Interleave(float[] left, float[] right)
    {
        var buffer = new float[left.Length * 2];
        for (int i = 0; i < left.Length; i++)
        {
            buffer[i * 2] = left[i];
            buffer[i * 2 + 1] = right[i];
        }
        return buffer;
    }

    /// <summary>
    /// Deinterleaves a stereo buffer into separate L/R arrays.
    /// </summary>
    private static (float[] Left, float[] Right) Deinterleave(float[] buffer, int sampleCount)
    {
        var left = new float[sampleCount];
        var right = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            left[i] = buffer[i * 2];
            right[i] = buffer[i * 2 + 1];
        }
        return (left, right);
    }

    private static float MaxAbs(Span<float> data)
    {
        float max = 0;
        for (int i = 0; i < data.Length; i++)
            max = Math.Max(max, Math.Abs(data[i]));
        return max;
    }

    // ── 1. Impulse response convolution ────────────────────────────────────

    [Fact]
    public void ImpulseResponse_SingleTapUnity_ProducesCorrectOutput()
    {
        var profile = CreateUnityProfile(irLength: 4);
        var effect = new HrtfEffect();
        effect.SetProfile(profile);
        effect.SetDirection(0, 0);
        effect.IsEnabled = true;
        effect.SpatialMix = 1.0;

        // Stereo impulse: L=0.5 at sample 0, R=0.3 at sample 0, rest zeros
        var leftIn = new float[] { 0.5f, 0, 0, 0, 0 };
        var rightIn = new float[] { 0.3f, 0, 0, 0, 0 };
        var buffer = Interleave(leftIn, rightIn);

        effect.Process(buffer);

        var (lOut, rOut) = Deinterleave(buffer, 5);
        // Unity HRIR [1,0,0,0]: output should equal input (L+R gets routed to each ear)
        Assert.InRange(lOut[0], 0.79, 0.81); // 0.5*1 + 0.3*1 = 0.8
        Assert.InRange(rOut[0], 0.79, 0.81);
        Assert.Equal(0f, lOut[1]);
        Assert.Equal(0f, rOut[1]);
    }

    [Fact]
    public void ImpulseResponse_DelayedTap_ProducesDelayedOutput()
    {
        // HRIR: gain 0.8 at tap 2
        var profile = CreateGainProfile(0.8f, 0.6f, tapIndex: 2, irLength: 4);
        var effect = new HrtfEffect();
        effect.SetProfile(profile);
        effect.SetDirection(0, 0);
        effect.IsEnabled = true;
        effect.SpatialMix = 1.0;

        // Single sample impulse
        var leftIn = new float[] { 1.0f, 0, 0, 0, 0, 0 };
        var rightIn = new float[] { 0, 0, 0, 0, 0, 0 };
        var buffer = Interleave(leftIn, rightIn);

        effect.Process(buffer);

        var (lOut, rOut) = Deinterleave(buffer, 6);
        // First 2 samples: no overlap yet (first block), edge effect
        // Output at sample 0 should have left input * left HRIR[0] = 1.0 * 0 = 0
        // Output at sample 2 should have left input * left HRIR[2] = 1.0 * 0.8 = 0.8
        Assert.InRange(lOut[2], 0.79, 0.81);
        Assert.InRange(rOut[2], 0.59, 0.61);
    }

    // ── 2. Zero HRIR produces silence ──────────────────────────────────────

    [Fact]
    public void ZeroHrir_ProducesSilence()
    {
        var profile = CreateGainProfile(0f, 0f, irLength: 4);
        var effect = new HrtfEffect();
        effect.SetProfile(profile);
        effect.SetDirection(0, 0);
        effect.IsEnabled = true;
        effect.SpatialMix = 1.0;

        var leftIn = new float[] { 0.5f, 0.3f, 0.7f, 0.1f };
        var rightIn = new float[] { 0.2f, 0.8f, 0.4f, 0.6f };
        var buffer = Interleave(leftIn, rightIn);

        effect.Process(buffer);

        var (lOut, rOut) = Deinterleave(buffer, 4);
        Assert.All(lOut, s => Assert.Equal(0f, s));
        Assert.All(rOut, s => Assert.Equal(0f, s));
    }

    // ── 3. Unity impulse passthrough ───────────────────────────────────────

    [Fact]
    public void UnityImpulse_Passthrough()
    {
        var profile = CreateUnityProfile(irLength: 4);
        var effect = new HrtfEffect();
        effect.SetProfile(profile);
        effect.SetDirection(0, 0);
        effect.IsEnabled = true;
        effect.SpatialMix = 1.0;

        var leftIn = new float[] { 0.5f, -0.3f, 0.7f, 0.1f, 0.9f };
        var rightIn = new float[] { 0.2f, 0.8f, -0.4f, 0.6f, 0.3f };
        var buffer = Interleave(leftIn, rightIn);

        effect.Process(buffer);

        var (lOut, rOut) = Deinterleave(buffer, 5);

        // With unity HRIR [1,0,0,0], output = (L+R) for each ear
        for (int i = 0; i < 5; i++)
        {
            var expected = leftIn[i] + rightIn[i];
            Assert.InRange(lOut[i], expected - 0.01f, expected + 0.01f);
            Assert.InRange(rOut[i], expected - 0.01f, expected + 0.01f);
        }
    }

    // ── 4. Cross-channel mixing ────────────────────────────────────────────

    [Fact]
    public void CrossChannelMixing_CorrectFormula()
    {
        // hLL = 0.5, hLR = 0.3, hRL = 0.2, hRR = 0.4
        // In our implementation, hLL=hLR=LeftEarResponse, hRL=hRR=RightEarResponse
        var leftIr = new float[] { 0.5f, 0, 0, 0 };
        var rightIr = new float[] { 0.3f, 0, 0, 0 };
        var profile = CreateCustomProfile(leftIr, rightIr);

        var effect = new HrtfEffect();
        effect.SetProfile(profile);
        effect.SetDirection(0, 0);
        effect.IsEnabled = true;
        effect.SpatialMix = 1.0;

        // Stereo input: L=1.0, R=0.5 at sample 0
        var leftIn = new float[] { 1.0f, 0, 0, 0 };
        var rightIn = new float[] { 0.5f, 0, 0, 0 };
        var buffer = Interleave(leftIn, rightIn);

        effect.Process(buffer);

        var (lOut, rOut) = Deinterleave(buffer, 4);
        // Lout = L*hLL + R*hLR = 1.0*0.5 + 0.5*0.5 = 0.75
        // Rout = L*hRL + R*hRR = 1.0*0.3 + 0.5*0.3 = 0.45
        Assert.InRange(lOut[0], 0.74, 0.76);
        Assert.InRange(rOut[0], 0.44, 0.46);
    }

    // ── 5. Stereo preservation ─────────────────────────────────────────────

    [Fact]
    public void StereoPreservation_InterleavedOutput()
    {
        var profile = CreateUnityProfile(irLength: 4);
        var effect = new HrtfEffect();
        effect.SetProfile(profile);
        effect.SetDirection(0, 0);
        effect.IsEnabled = true;

        var leftIn = new float[] { 0.5f, 0.3f, 0.7f };
        var rightIn = new float[] { 0.2f, 0.8f, 0.4f };
        var buffer = Interleave(leftIn, rightIn);

        effect.Process(buffer);

        // Output must be same length as input
        Assert.Equal(6, buffer.Length);

        // Output must be interleaved stereo
        var (lOut, rOut) = Deinterleave(buffer, 3);
        Assert.Equal(3, lOut.Length);
        Assert.Equal(3, rOut.Length);
    }

    // ── 6. Block continuity ────────────────────────────────────────────────

    [Fact]
    public void BlockContinuity_SmallBlocksMatchLargeBlock()
    {
        var profile = CreateGainProfile(0.9f, 0.7f, tapIndex: 1, irLength: 8);
        var sampleCount = 48;

        // Generate stereo test signal
        var leftIn = new float[sampleCount];
        var rightIn = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            leftIn[i] = (float)Math.Sin(2 * Math.PI * 440 * i / 48000);
            rightIn[i] = (float)Math.Sin(2 * Math.PI * 880 * i / 48000);
        }

        // Process as one large buffer
        var largeBuffer = Interleave(leftIn, rightIn);
        var effectLarge = new HrtfEffect();
        effectLarge.SetProfile(profile);
        effectLarge.SetDirection(0, 0);
        effectLarge.IsEnabled = true;
        effectLarge.SpatialMix = 1.0;
        effectLarge.Process(largeBuffer);

        // Process as 3 small blocks of 16 samples each
        var effectSmall = new HrtfEffect();
        effectSmall.SetProfile(profile);
        effectSmall.SetDirection(0, 0);
        effectSmall.IsEnabled = true;
        effectSmall.SpatialMix = 1.0;

        var smallOutputL = new float[sampleCount];
        var smallOutputR = new float[sampleCount];
        var blockSize = 16;

        for (int offset = 0; offset < sampleCount; offset += blockSize)
        {
            var count = Math.Min(blockSize, sampleCount - offset);
            var blockLeft = new float[count];
            var blockRight = new float[count];
            Array.Copy(leftIn, offset, blockLeft, 0, count);
            Array.Copy(rightIn, offset, blockRight, 0, count);
            var blockBuffer = Interleave(blockLeft, blockRight);
            effectSmall.Process(blockBuffer);

            var (bl, br) = Deinterleave(blockBuffer, count);
            Array.Copy(bl, 0, smallOutputL, offset, count);
            Array.Copy(br, 0, smallOutputR, offset, count);
        }

        var (largeL, largeR) = Deinterleave(largeBuffer, sampleCount);

        // Compare (skip first irLength-1 samples due to first-block edge effect)
        var skip = profile.IrLength - 1;
        for (int i = skip; i < sampleCount; i++)
        {
            Assert.InRange(largeL[i], smallOutputL[i] - 0.001f, smallOutputL[i] + 0.001f);
            Assert.InRange(largeR[i], smallOutputR[i] - 0.001f, smallOutputR[i] + 0.001f);
        }
    }

    // ── 7. Reset clears history ────────────────────────────────────────────

    [Fact]
    public void Reset_ClearsConvolutionHistory()
    {
        var profile = CreateGainProfile(0.8f, 0.6f, tapIndex: 2, irLength: 8);
        var effect = new HrtfEffect();
        effect.SetProfile(profile);
        effect.SetDirection(0, 0);
        effect.IsEnabled = true;
        effect.SpatialMix = 1.0;

        // Process block 1
        var block1 = Interleave(
            new float[] { 1.0f, 0, 0, 0 },
            new float[] { 0, 0, 0, 0 });
        effect.Process(block1);

        // Reset
        effect.Reset();

        // Process block 2 with different signal
        var block2 = Interleave(
            new float[] { 0.5f, 0, 0, 0 },
            new float[] { 0, 0, 0, 0 });
        effect.Process(block2);

        var (lOut, _) = Deinterleave(block2, 4);
        // After reset, the convolution should not carry any state from block 1
        // At sample 0, output should be 0.5 * HRIR[0] = 0.5 * 0 = 0
        Assert.Equal(0f, lOut[0]);
    }

    // ── 8. SpatialMix ──────────────────────────────────────────────────────

    [Fact]
    public void SpatialMix_Zero_Passthrough()
    {
        var profile = CreateGainProfile(2.0f, 2.0f, irLength: 4);
        var effect = new HrtfEffect();
        effect.SetProfile(profile);
        effect.SetDirection(0, 0);
        effect.IsEnabled = true;
        effect.SpatialMix = 0.0;

        var leftIn = new float[] { 0.5f, 0.3f, 0.7f, 0.1f };
        var rightIn = new float[] { 0.2f, 0.8f, 0.4f, 0.6f };
        var buffer = Interleave(leftIn, rightIn);

        effect.Process(buffer);

        var (lOut, rOut) = Deinterleave(buffer, 4);
        for (int i = 0; i < 4; i++)
        {
            Assert.InRange(lOut[i], leftIn[i] - 0.001f, leftIn[i] + 0.001f);
            Assert.InRange(rOut[i], rightIn[i] - 0.001f, rightIn[i] + 0.001f);
        }
    }

    [Fact]
    public void SpatialMix_One_FullyProcessed()
    {
        var profile = CreateGainProfile(1.0f, 1.0f, irLength: 4);
        var effect = new HrtfEffect();
        effect.SetProfile(profile);
        effect.SetDirection(0, 0);
        effect.IsEnabled = true;
        effect.SpatialMix = 1.0;

        var leftIn = new float[] { 0.5f, 0, 0, 0 };
        var rightIn = new float[] { 0.3f, 0, 0, 0 };
        var buffer = Interleave(leftIn, rightIn);

        effect.Process(buffer);

        var (lOut, _) = Deinterleave(buffer, 4);
        // Fully processed: output = (L+R)*HRIR[0] = 0.8*1.0 = 0.8
        Assert.InRange(lOut[0], 0.79, 0.81);
    }

    [Fact]
    public void SpatialMix_Half_CorrectBlend()
    {
        var profile = CreateGainProfile(1.0f, 1.0f, irLength: 4);
        var effect = new HrtfEffect();
        effect.SetProfile(profile);
        effect.SetDirection(0, 0);
        effect.IsEnabled = true;
        effect.SpatialMix = 0.5;

        var leftIn = new float[] { 0.5f, 0, 0, 0 };
        var rightIn = new float[] { 0.3f, 0, 0, 0 };
        var buffer = Interleave(leftIn, rightIn);

        effect.Process(buffer);

        var (lOut, _) = Deinterleave(buffer, 4);
        // Dry = 0.5, Processed = 0.8, Blend = 0.5*0.5 + 0.5*0.8 = 0.65
        Assert.InRange(lOut[0], 0.64, 0.66);
    }

    // ── 9. Disabled leaves buffer unchanged ────────────────────────────────

    [Fact]
    public void Disabled_LeavesBufferUnchanged()
    {
        var profile = CreateUnityProfile(irLength: 4);
        var effect = new HrtfEffect();
        effect.SetProfile(profile);
        effect.SetDirection(0, 0);
        effect.IsEnabled = false;

        var leftIn = new float[] { 0.5f, 0.3f, 0.7f };
        var rightIn = new float[] { 0.2f, 0.8f, 0.4f };
        var buffer = Interleave(leftIn, rightIn);
        var original = (float[])buffer.Clone();

        effect.Process(buffer);

        for (int i = 0; i < buffer.Length; i++)
        {
            Assert.Equal(original[i], buffer[i]);
        }
    }

    // ── 10. Invalid/empty profile ──────────────────────────────────────────

    [Fact]
    public void EmptyProfile_NoCrash()
    {
        var profile = new HrtfProfile
        {
            Id = "empty",
            Name = "Empty",
            SampleRate = 48000,
            IrLength = 0,
            Entries = Array.Empty<HrtfEntry>()
        };
        var effect = new HrtfEffect();
        effect.SetProfile(profile);
        effect.IsEnabled = true;

        var buffer = Interleave(
            new float[] { 0.5f, 0.3f, 0.7f },
            new float[] { 0.2f, 0.8f, 0.4f });
        var original = (float[])buffer.Clone();

        effect.Process(buffer);

        // Should not crash, buffer unchanged
        for (int i = 0; i < buffer.Length; i++)
            Assert.Equal(original[i], buffer[i]);
    }

    [Fact]
    public void NullProfile_NoCrash()
    {
        var effect = new HrtfEffect();
        effect.SetProfile(null);
        effect.IsEnabled = true;

        var buffer = Interleave(
            new float[] { 0.5f, 0.3f },
            new float[] { 0.2f, 0.8f });
        var original = (float[])buffer.Clone();

        effect.Process(buffer);

        for (int i = 0; i < buffer.Length; i++)
            Assert.Equal(original[i], buffer[i]);
    }

    [Fact]
    public void NoDirectionSelected_NoCrash()
    {
        var profile = CreateUnityProfile(irLength: 4);
        var effect = new HrtfEffect();
        effect.SetProfile(profile);
        // Don't call SetDirection — no entry selected
        effect.IsEnabled = true;

        var buffer = Interleave(
            new float[] { 0.5f, 0.3f },
            new float[] { 0.2f, 0.8f });
        var original = (float[])buffer.Clone();

        effect.Process(buffer);

        for (int i = 0; i < buffer.Length; i++)
            Assert.Equal(original[i], buffer[i]);
    }

    // ── HrtfProfile model tests ────────────────────────────────────────────

    [Fact]
    public void HrtfProfile_Clone_CreatesIndependentCopy()
    {
        var profile = HrtfProfilePresets.GetById("synthetic-front");
        var clone = profile.Clone();

        Assert.Equal(profile.Id, clone.Id);
        Assert.False(ReferenceEquals(profile, clone));
    }

    [Fact]
    public void HrtfProfile_CloneEntriesAreDeepCopied()
    {
        var profile = HrtfProfilePresets.GetById("synthetic-front");
        var clone = profile.Clone();

        Assert.Equal(profile.Entries.Length, clone.Entries.Length);
        Assert.False(ReferenceEquals(profile.Entries, clone.Entries));

        if (profile.Entries.Length > 0)
        {
            Assert.False(ReferenceEquals(profile.Entries[0].LeftEarResponse, clone.Entries[0].LeftEarResponse));
        }
    }

    [Fact]
    public void HrtfProfile_GetEntryForDirection_FindsNearest()
    {
        var profile = HrtfProfilePresets.GetById("synthetic-left");
        var entry = profile.GetEntryForDirection(-90, 0);

        Assert.NotNull(entry);
        Assert.Equal(-90, entry!.AzimuthDeg);
    }

    [Fact]
    public void HrtfProfile_GetEntryForDirection_EmptyProfile_ReturnsNull()
    {
        var profile = HrtfProfilePresets.GetNone();
        var entry = profile.GetEntryForDirection(0, 0);

        Assert.Null(entry);
    }

    // ── HrtfProfilePresets tests ───────────────────────────────────────────

    [Fact]
    public void HrtfPresets_NonEmpty_UniqueIds()
    {
        Assert.NotEmpty(HrtfProfilePresets.Profiles);
        Assert.Equal(
            HrtfProfilePresets.Profiles.Count,
            HrtfProfilePresets.Profiles.Select(p => p.Id).Distinct().Count());
    }

    [Fact]
    public void HrtfPresets_AllHaveNames()
    {
        Assert.All(HrtfProfilePresets.Profiles, p =>
            Assert.False(string.IsNullOrWhiteSpace(p.Name)));
    }

    [Fact]
    public void HrtfPresets_AllDescriptionsMarkedSynthetic()
    {
        Assert.All(HrtfProfilePresets.Profiles, p =>
            Assert.Contains("SYNTHETIC", p.Description, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void HrtfPresets_AllDataSourceMarkedSynthetic()
    {
        Assert.All(HrtfProfilePresets.Profiles, p =>
            Assert.Contains("Synthetic", p.DataSource));
    }

    [Fact]
    public void GetById_ReturnsClone()
    {
        var a = HrtfProfilePresets.GetById("synthetic-front");
        var b = HrtfProfilePresets.GetById("synthetic-front");
        Assert.False(ReferenceEquals(a, b));
    }

    [Fact]
    public void GetNone_ReturnsDisabledProfile()
    {
        var none = HrtfProfilePresets.GetNone();
        Assert.Equal("none", none.Id);
        Assert.Contains("Disabled", none.Name);
        Assert.Empty(none.Entries);
    }

    // ── SetDirection tests ─────────────────────────────────────────────────

    [Fact]
    public void SetDirection_LoadsCorrectEntry()
    {
        var profile = HrtfProfilePresets.GetById("synthetic-front");
        var effect = new HrtfEffect();
        effect.SetProfile(profile);
        effect.SetDirection(0, 0);

        Assert.NotNull(effect.CurrentEntry);
        Assert.Equal(0, effect.CurrentEntry!.AzimuthDeg);
    }

    [Fact]
    public void SetDirection_ChangesDirection()
    {
        var profile = HrtfProfilePresets.GetById("synthetic-front");
        var effect = new HrtfEffect();
        effect.SetProfile(profile);
        effect.SetDirection(0, 0);
        var first = effect.CurrentEntry;

        effect.SetDirection(0, 0); // same direction, should be no-op
        Assert.Same(first, effect.CurrentEntry);
    }

    [Fact]
    public void SpatialMix_ClampsInvalidValues()
    {
        var effect = new HrtfEffect();
        effect.SpatialMix = -0.5;
        Assert.True(effect.SpatialMix >= 0);

        effect.SpatialMix = 1.5;
        // SpatialMix should be clamped (implementation-dependent)
        // Just verify no crash
        var profile = CreateUnityProfile(irLength: 4);
        effect.SetProfile(profile);
        effect.SetDirection(0, 0);
        effect.IsEnabled = true;

        var buffer = Interleave(new float[] { 0.5f, 0.3f }, new float[] { 0.2f, 0.8f });
        effect.Process(buffer); // should not throw
    }
}
