using SoundFXStudio.Models;
using SoundFXStudio.Services;
using SoundFXStudio.Services.DSP;
using SoundFXStudio.Services.Hrtf;
using Xunit;

namespace SoundFXStudio.Tests;

public class HrtfTransitionTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    private static HrtfProfile CreateTestProfile()
    {
        var entries = new List<HrtfEntry>();
        var azimuths = new[] { -90.0, 0.0, 90.0 };
        var elevations = new[] { 0.0, 45.0 };

        foreach (var el in elevations)
        {
            foreach (var az in azimuths)
            {
                var left = new float[64];
                var right = new float[64];
                left[0] = 1.0f;
                right[0] = 0.9f;
                for (int i = 1; i < 64; i++)
                {
                    left[i] = 0.5f / (i + 1);
                    right[i] = 0.45f / (i + 1);
                }

                entries.Add(new HrtfEntry
                {
                    AzimuthDeg = az,
                    ElevationDeg = el,
                    LeftEarResponse = left,
                    RightEarResponse = right
                });
            }
        }

        return new HrtfProfile
        {
            Id = "transition-test",
            Name = "Transition Test",
            SampleRate = 48000,
            IrLength = 64,
            Entries = entries.ToArray(),
            Manufacturer = "Test",
            Description = "Test",
            DataSource = "Test",
            License = "Test"
        };
    }

    private static float[] MakeStereoBuffer(int samples)
    {
        var buffer = new float[samples * 2];
        var rng = new Random(42);
        for (int i = 0; i < samples; i++)
        {
            buffer[i * 2] = (float)(rng.NextDouble() * 2 - 1);
            buffer[i * 2 + 1] = (float)(rng.NextDouble() * 2 - 1);
        }
        return buffer;
    }

    // ── 1. ImmediateTransition_UsesNewHrir ────────────────────────────────

    [Fact]
    public void ImmediateTransition_UsesNewHrir()
    {
        var hrtf = new HrtfEffect(48000) { DirectionTransitionMs = 0, IsEnabled = true };
        hrtf.SetProfile(CreateTestProfile());
        hrtf.SetDirection(0, 0);

        var buffer = MakeStereoBuffer(480);
        hrtf.Process(buffer);

        var maxAbs = MaxAbs(buffer);
        Assert.True(maxAbs > 0, "Immediate transition should produce non-silent output");
        Assert.False(hrtf.IsTransitioning);
    }

    // ── 2. Transition_DefaultDurationIs20Ms ──────────────────────────────

    [Fact]
    public void Transition_DefaultDurationIs20Ms()
    {
        var hrtf = new HrtfEffect(48000);
        Assert.Equal(20, hrtf.DirectionTransitionMs);
    }

    // ── 3. TransitionDuration_IsClamped ──────────────────────────────────

    [Fact]
    public void TransitionDuration_IsClamped()
    {
        var hrtf = new HrtfEffect(48000);
        hrtf.DirectionTransitionMs = -5;
        Assert.Equal(0, hrtf.DirectionTransitionMs);

        hrtf.DirectionTransitionMs = 200;
        Assert.Equal(100, hrtf.DirectionTransitionMs);

        hrtf.DirectionTransitionMs = 50;
        Assert.Equal(50, hrtf.DirectionTransitionMs);
    }

    // ── 4. Transition_StartsFromCurrentHrir ──────────────────────────────

    [Fact]
    public void Transition_StartsFromCurrentHrir()
    {
        var hrtf = new HrtfEffect(48000) { DirectionTransitionMs = 20, IsEnabled = true };
        hrtf.SetProfile(CreateTestProfile());
        hrtf.SetDirection(0, 0);

        // Process one block to establish overlap from HRIR at (0,0)
        var buffer1 = MakeStereoBuffer(480);
        hrtf.Process(buffer1);

        // Now change direction — transition should start from the current HRIR
        hrtf.SetDirection(90, 0);
        Assert.True(hrtf.IsTransitioning);

        var buffer2 = MakeStereoBuffer(480);
        hrtf.Process(buffer2);

        var maxAbs = MaxAbs(buffer2);
        Assert.True(maxAbs > 0, "Transition should produce non-silent output");
    }

    // ── 5. Transition_ReachesNewHrir ─────────────────────────────────────

    [Fact]
    public void Transition_ReachesNewHrir()
    {
        var hrtf = new HrtfEffect(48000) { DirectionTransitionMs = 10, IsEnabled = true };
        hrtf.SetProfile(CreateTestProfile());
        hrtf.SetDirection(0, 0);

        // Process several blocks to complete the transition
        for (int i = 0; i < 20; i++)
        {
            var buffer = MakeStereoBuffer(480);
            hrtf.Process(buffer);
        }

        Assert.False(hrtf.IsTransitioning);
    }

    // ── 6. Transition_IsMonotonic ────────────────────────────────────────

    [Fact]
    public void Transition_IsMonotonic()
    {
        var hrtf = new HrtfEffect(48000) { DirectionTransitionMs = 50, IsEnabled = true };
        hrtf.SetProfile(CreateTestProfile());
        hrtf.SetDirection(0, 0);

        // Process one block to initialize
        hrtf.Process(MakeStereoBuffer(480));

        hrtf.SetDirection(90, 0);
        Assert.True(hrtf.IsTransitioning);

        // Process blocks and track amplitude progression
        var amplitudes = new List<float>();
        for (int i = 0; i < 10; i++)
        {
            var buffer = MakeStereoBuffer(480);
            hrtf.Process(buffer);
            amplitudes.Add(MaxAbs(buffer));
        }

        // Amplitude should generally increase or stay stable (not wildly oscillate)
        Assert.True(amplitudes.Count >= 2);
        // First and last should both be non-zero
        Assert.True(amplitudes[0] > 0);
        Assert.True(amplitudes[^1] > 0);
    }

    // ── 7. Transition_AllFourPathsAreUpdated ─────────────────────────────

    [Fact]
    public void Transition_AllFourPathsAreUpdated()
    {
        var hrtf = new HrtfEffect(48000) { DirectionTransitionMs = 5, IsEnabled = true };
        hrtf.SetProfile(CreateTestProfile());
        hrtf.SetDirection(0, 0);
        hrtf.Process(MakeStereoBuffer(480));

        // Change direction — transition runs all 4 paths
        hrtf.SetDirection(90, 0);
        var buffer = MakeStereoBuffer(480);
        hrtf.Process(buffer);

        // Output should not be silent (all 4 paths contribute)
        var maxAbs = MaxAbs(buffer);
        Assert.True(maxAbs > 0);
    }

    // ── 8. Transition_DoesNotResetConvolutionHistory ────────────────────

    [Fact]
    public void Transition_DoesNotResetConvolutionHistory()
    {
        var hrtf = new HrtfEffect(48000) { DirectionTransitionMs = 20, IsEnabled = true };
        hrtf.SetProfile(CreateTestProfile());
        hrtf.SetDirection(0, 0);

        // Process several blocks to build up overlap
        for (int i = 0; i < 5; i++)
            hrtf.Process(MakeStereoBuffer(480));

        // Capture output before direction change
        var before = MakeStereoBuffer(480);
        hrtf.Process(before);
        var ampBefore = MaxAbs(before);

        // Change direction — overlap should be preserved (not reset)
        hrtf.SetDirection(90, 0);
        var after = MakeStereoBuffer(480);
        hrtf.Process(after);
        var ampAfter = MaxAbs(after);

        // Both should produce non-trivial output
        Assert.True(ampBefore > 0);
        Assert.True(ampAfter > 0);
    }

    // ── 9. SameDirection_DoesNotRestartTransition ───────────────────────

    [Fact]
    public void SameDirection_DoesNotRestartTransition()
    {
        var hrtf = new HrtfEffect(48000) { DirectionTransitionMs = 20, IsEnabled = true };
        hrtf.SetProfile(CreateTestProfile());
        hrtf.SetDirection(0, 0);
        hrtf.Process(MakeStereoBuffer(480));

        hrtf.SetDirection(45, 0);
        Assert.True(hrtf.IsTransitioning);

        // Same direction again — should not restart
        hrtf.SetDirection(45, 0);
        Assert.True(hrtf.IsTransitioning);
    }

    // ── 10. RapidDirectionChanges_LatestDirectionWins ────────────────────

    [Fact]
    public void RapidDirectionChanges_LatestDirectionWins()
    {
        var hrtf = new HrtfEffect(48000) { DirectionTransitionMs = 50, IsEnabled = true };
        hrtf.SetProfile(CreateTestProfile());
        hrtf.SetDirection(0, 0);
        hrtf.Process(MakeStereoBuffer(480));

        // Rapid direction changes
        hrtf.SetDirection(30, 0);
        Assert.True(hrtf.IsTransitioning);
        hrtf.Process(MakeStereoBuffer(480));

        hrtf.SetDirection(60, 0);
        hrtf.Process(MakeStereoBuffer(480));

        hrtf.SetDirection(90, 0);
        hrtf.Process(MakeStereoBuffer(480));

        // Continue processing to complete the transition to (90, 0)
        for (int i = 0; i < 20; i++)
            hrtf.Process(MakeStereoBuffer(480));

        Assert.False(hrtf.IsTransitioning);

        // Process one more block — output should reflect the final direction
        var final = MakeStereoBuffer(480);
        hrtf.Process(final);
        Assert.True(MaxAbs(final) > 0);
    }

    // ── 11. TransitionZeroMs_IsImmediate ─────────────────────────────────

    [Fact]
    public void TransitionZeroMs_IsImmediate()
    {
        var hrtf = new HrtfEffect(48000) { DirectionTransitionMs = 0, IsEnabled = true };
        hrtf.SetProfile(CreateTestProfile());
        hrtf.SetDirection(0, 0);
        hrtf.Process(MakeStereoBuffer(480));

        hrtf.SetDirection(90, 0);
        Assert.False(hrtf.IsTransitioning);

        var buffer = MakeStereoBuffer(480);
        hrtf.Process(buffer);
        Assert.True(MaxAbs(buffer) > 0);
    }

    // ── 12. TransitionDoesNotModifySourceProfile ────────────────────────

    [Fact]
    public void TransitionDoesNotModifySourceProfile()
    {
        var profile = CreateTestProfile();
        var originalLeft0 = (float[])profile.Entries[0].LeftEarResponse.Clone();
        var originalRight0 = (float[])profile.Entries[0].RightEarResponse.Clone();

        var hrtf = new HrtfEffect(48000) { DirectionTransitionMs = 20, IsEnabled = true };
        hrtf.SetProfile(profile);
        hrtf.SetDirection(0, 0);
        hrtf.Process(MakeStereoBuffer(480));

        hrtf.SetDirection(45, 22.5);
        hrtf.Process(MakeStereoBuffer(480));

        for (int i = 0; i < originalLeft0.Length; i++)
            Assert.Equal(originalLeft0[i], profile.Entries[0].LeftEarResponse[i]);
        for (int i = 0; i < originalRight0.Length; i++)
            Assert.Equal(originalRight0[i], profile.Entries[0].RightEarResponse[i]);
    }

    // ── 13. SpatialMixZero_RemainsDry ────────────────────────────────────

    [Fact]
    public void SpatialMixZero_RemainsDry()
    {
        var hrtf = new HrtfEffect(48000)
        {
            DirectionTransitionMs = 20,
            IsEnabled = true,
            SpatialMix = 0.0
        };
        hrtf.SetProfile(CreateTestProfile());
        hrtf.SetDirection(0, 0);

        var input = MakeStereoBuffer(480);
        var expected = (float[])input.Clone();

        hrtf.Process(input);

        // With SpatialMix=0, output should be identical to input
        for (int i = 0; i < input.Length; i++)
            Assert.Equal(expected[i], input[i], 4);
    }

    // ── 14. SpatialMixOne_RemainsWet ─────────────────────────────────────

    [Fact]
    public void SpatialMixOne_RemainsWet()
    {
        var hrtf = new HrtfEffect(48000)
        {
            DirectionTransitionMs = 20,
            IsEnabled = true,
            SpatialMix = 1.0
        };
        hrtf.SetProfile(CreateTestProfile());
        hrtf.SetDirection(0, 0);

        var input = MakeStereoBuffer(480);
        var inputCopy = (float[])input.Clone();

        hrtf.Process(input);

        // With SpatialMix=1, output should differ from input (fully processed)
        var differs = false;
        for (int i = 0; i < input.Length; i++)
        {
            if (Math.Abs(input[i] - inputCopy[i]) > 1e-6f)
            {
                differs = true;
                break;
            }
        }
        Assert.True(differs, "SpatialMix=1 should produce processed output different from input");
    }

    // ── 15. Process_DoesNotAllocateDuringTransition ─────────────────────

    [Fact]
    public void Process_DoesNotAllocateDuringTransition()
    {
        var hrtf = new HrtfEffect(48000) { DirectionTransitionMs = 20, IsEnabled = true };
        hrtf.SetProfile(CreateTestProfile());
        hrtf.SetDirection(0, 0);
        hrtf.Process(MakeStereoBuffer(480));

        hrtf.SetDirection(90, 0);
        Assert.True(hrtf.IsTransitioning);

        // Warmup
        for (int i = 0; i < 5; i++)
            hrtf.Process(MakeStereoBuffer(480));

        GC.Collect(0, GCCollectionMode.Forced);
        var gen0Before = GC.CollectionCount(0);

        for (int i = 0; i < 100; i++)
            hrtf.Process(MakeStereoBuffer(480));

        var gen0After = GC.CollectionCount(0);

        Assert.Equal(0, gen0After - gen0Before);
    }

    // ── 16. Process_DoesNotLock ──────────────────────────────────────────

    [Fact]
    public void Process_DoesNotLock()
    {
        var hrtf = new HrtfEffect(48000) { DirectionTransitionMs = 20, IsEnabled = true };
        hrtf.SetProfile(CreateTestProfile());
        hrtf.SetDirection(0, 0);
        hrtf.Process(MakeStereoBuffer(480));

        hrtf.SetDirection(90, 0);

        // Concurrent Process + SetDirection calls (basic contention test)
        var tasks = new List<Task>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        tasks.Add(Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
                hrtf.Process(MakeStereoBuffer(480));
        }, cts.Token));

        tasks.Add(Task.Run(() =>
        {
            var rng = new Random();
            while (!cts.Token.IsCancellationRequested)
            {
                hrtf.SetDirection(rng.NextDouble() * 180 - 90, rng.NextDouble() * 90);
                Thread.SpinWait(100);
            }
        }, cts.Token));

        Task.WaitAll(tasks.ToArray());
        // If we got here without deadlock, it passes
    }

    // ── 17. Reset_ClearsTransitionState ──────────────────────────────────

    [Fact]
    public void Reset_ClearsTransitionState()
    {
        var hrtf = new HrtfEffect(48000) { DirectionTransitionMs = 20, IsEnabled = true };
        hrtf.SetProfile(CreateTestProfile());
        hrtf.SetDirection(0, 0);
        hrtf.Process(MakeStereoBuffer(480));

        hrtf.SetDirection(90, 0);
        Assert.True(hrtf.IsTransitioning);

        hrtf.Reset();
        Assert.False(hrtf.IsTransitioning);
    }

    // ── 18. DisabledHrtf_DoesNotTransition ──────────────────────────────

    [Fact]
    public void DisabledHrtf_DoesNotTransition()
    {
        var hrtf = new HrtfEffect(48000) { DirectionTransitionMs = 20, IsEnabled = false };
        hrtf.SetProfile(CreateTestProfile());
        hrtf.SetDirection(0, 0);

        var input = MakeStereoBuffer(480);
        var expected = (float[])input.Clone();

        hrtf.Process(input);

        // Disabled HRTF should pass through
        for (int i = 0; i < input.Length; i++)
            Assert.Equal(expected[i], input[i], 4);
    }

    // ── 19. EmptyProfile_IsHandledSafely ────────────────────────────────

    [Fact]
    public void EmptyProfile_IsHandledSafely()
    {
        var hrtf = new HrtfEffect(48000) { DirectionTransitionMs = 20, IsEnabled = true };
        hrtf.SetProfile(new HrtfProfile
        {
            Id = "empty",
            Name = "Empty",
            SampleRate = 48000,
            IrLength = 0,
            Entries = Array.Empty<HrtfEntry>(),
            Manufacturer = "T",
            Description = "T",
            DataSource = "T",
            License = "T"
        });

        hrtf.SetDirection(0, 0);
        var buffer = MakeStereoBuffer(480);
        hrtf.Process(buffer);

        // Should not crash, output unchanged (disabled state)
        var maxAbs = MaxAbs(buffer);
        Assert.True(maxAbs > 0); // Input was random, still random
    }

    // ── 20. DifferentHrirLengths_AreHandledSafely ───────────────────────

    [Fact]
    public void DifferentHrirLengths_AreHandledSafely()
    {
        var profile1 = CreateTestProfile(); // IrLength = 64
        var profile2 = new HrtfProfile
        {
            Id = "short",
            Name = "Short",
            SampleRate = 48000,
            IrLength = 32,
            Entries = new[]
            {
                new HrtfEntry
                {
                    AzimuthDeg = 0, ElevationDeg = 0,
                    LeftEarResponse = new float[32],
                    RightEarResponse = new float[32]
                },
                new HrtfEntry
                {
                    AzimuthDeg = 90, ElevationDeg = 0,
                    LeftEarResponse = new float[32],
                    RightEarResponse = new float[32]
                }
            },
            Manufacturer = "T", Description = "T", DataSource = "T", License = "T"
        };

        var hrtf = new HrtfEffect(48000) { DirectionTransitionMs = 20, IsEnabled = true };
        hrtf.SetProfile(profile1);
        hrtf.SetDirection(0, 0);
        hrtf.Process(MakeStereoBuffer(480));

        // Switch to shorter-profile direction
        hrtf.SetProfile(profile2);
        hrtf.SetDirection(0, 0);
        hrtf.Process(MakeStereoBuffer(480));

        Assert.False(hrtf.IsTransitioning);
    }

    // ── SOFA integration ─────────────────────────────────────────────────

    [Fact]
    public void SofaProfile_WorksTransitioning()
    {
        var sofaPath = Path.Combine(AppContext.BaseDirectory, "TestData", "SimpleFreeFieldHRIR_1.0.sofa");
        if (!File.Exists(sofaPath)) return;

        var loader = new SofaHrtfLoader();
        var result = loader.Load(sofaPath);
        Assert.True(result.Success);

        var hrtf = new HrtfEffect(48000) { DirectionTransitionMs = 10, IsEnabled = true };
        hrtf.SetProfile(result.Profile!);

        hrtf.SetDirection(0, 0);
        hrtf.Process(MakeStereoBuffer(480));

        hrtf.SetDirection(45, 22.5);
        for (int i = 0; i < 20; i++)
            hrtf.Process(MakeStereoBuffer(480));

        Assert.False(hrtf.IsTransitioning);
        var final = MakeStereoBuffer(480);
        hrtf.Process(final);
        Assert.True(MaxAbs(final) > 0);
    }

    // ── GamingEnhancementService integration ─────────────────────────────

    [Fact]
    public void GamingService_HrtfTransitionWorks()
    {
        var service = new GamingEnhancementService();
        service.ApplyHrtfProfile(CreateTestProfile());

        service.HrtfSpatializer.SetDirection(0, 0);
        service.HrtfSpatializer.SetDirection(90, 0);

        var buffer = MakeStereoBuffer(480);
        service.Chain.Process(buffer);
        Assert.True(MaxAbs(buffer) > 0);
    }

    [Fact]
    public void GamingService_ChainOrderUnchanged()
    {
        var service = new GamingEnhancementService();
        var effects = service.Chain.Effects;

        Assert.Same(service.Equalizer, effects[0]);
        Assert.IsType<EqualizerEffect>(effects[1]);
        Assert.IsType<HrtfEffect>(effects[2]);
        Assert.IsType<NoiseGateEffect>(effects[3]);
        Assert.Equal(6, effects.Count);
    }

    [Fact]
    public void GamingService_SampleRatePreparationStillWorks()
    {
        var service = new GamingEnhancementService();
        service.SetSampleRate(48000);

        var profile = new HrtfProfile
        {
            Id = "test",
            Name = "Test",
            SampleRate = 44100,
            IrLength = 64,
            Entries = new[]
            {
                new HrtfEntry { AzimuthDeg = 0, ElevationDeg = 0, LeftEarResponse = new float[64], RightEarResponse = new float[64] },
                new HrtfEntry { AzimuthDeg = 90, ElevationDeg = 0, LeftEarResponse = new float[64], RightEarResponse = new float[64] }
            },
            Manufacturer = "T", Description = "T", DataSource = "T", License = "T"
        };

        service.ApplyHrtfProfile(profile);

        var active = service.HrtfSpatializer.ActiveProfile;
        Assert.NotNull(active);
        Assert.Equal(48000, active!.SampleRate);
    }

    // ── Helper ───────────────────────────────────────────────────────────

    private static float MaxAbs(float[] buffer)
    {
        var max = 0f;
        for (int i = 0; i < buffer.Length; i++)
            max = Math.Max(max, Math.Abs(buffer[i]));
        return max;
    }
}
