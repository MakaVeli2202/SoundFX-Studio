using System.Diagnostics;
using SoundFXStudio.Models;
using SoundFXStudio.Services;
using SoundFXStudio.Services.DSP;

namespace SoundFXStudio.Tests.Performance;

/// <summary>
/// Lightweight internal benchmark for HRTF convolution and full gaming DSP chain.
/// Uses Stopwatch + GC.GetAllocatedBytesForCurrentThread for measurement.
/// Block-size convention: FramesPerChannel (mono frame count per channel).
/// Stereo interleaved buffer = FramesPerChannel × 2 floats.
/// </summary>
public static class HrtfPerformanceBenchmark
{
    private const int DefaultSampleRate = 48000;
    private const int DefaultWarmupIterations = 200;
    private const int DefaultMeasuredIterations = 2000;

    // ── Public API ─────────────────────────────────────────────────────

    public static List<HrtfBenchmarkResult> RunAll()
    {
        var results = new List<HrtfBenchmarkResult>();

        int[] blockSizes = [128, 256, 480, 960, 1920];
        int[] hrtfOnlyHrirLengths = [64, 128, 256, 512];

        // HRTF effect only — disabled baseline per block size
        foreach (var frames in blockSizes)
        {
            results.Add(BenchmarkHrtfEffectOnly(frames, 0, hrtfEnabled: false));
        }

        // HRTF effect only — each HRIR length × each block size
        foreach (var frames in blockSizes)
        {
            foreach (var irLen in hrtfOnlyHrirLengths)
            {
                results.Add(BenchmarkHrtfEffectOnly(frames, irLen, hrtfEnabled: true));
            }
        }

        // Full gaming chain — 480 and 960 frames
        int[] chainBlockSizes = [480, 960];
        int[] chainHrirLengths = [128, 256, 512];

        // Disabled baseline
        foreach (var frames in chainBlockSizes)
        {
            results.Add(BenchmarkFullChain(frames, 0, hrtfEnabled: false));
        }

        // Enabled with each HRIR
        foreach (var frames in chainBlockSizes)
        {
            foreach (var irLen in chainHrirLengths)
            {
                results.Add(BenchmarkFullChain(frames, irLen, hrtfEnabled: true));
            }
        }

        return results;
    }

    // ── HRTF Effect Only ───────────────────────────────────────────────

    private static HrtfBenchmarkResult BenchmarkHrtfEffectOnly(
        int framesPerChannel, int hrirLength, bool hrtfEnabled)
    {
        var sampleRate = DefaultSampleRate;
        var bufferLength = framesPerChannel * 2;
        var scenario = hrtfEnabled
            ? $"HRTF Only {hrirLength} taps"
            : "HRTF Only Disabled";

        var hrtf = new HrtfEffect(sampleRate);
        if (hrirLength > 0)
        {
            var profile = CreateSyntheticProfile(hrirLength);
            hrtf.SetProfile(profile);
            hrtf.SetDirection(0, 0);
        }
        hrtf.IsEnabled = hrtfEnabled;

        var input = CreateDeterministicInput(bufferLength);

        return RunBenchmark(scenario, sampleRate, framesPerChannel, hrirLength, hrtf, input);
    }

    // ── Full Gaming Chain ──────────────────────────────────────────────

    private static HrtfBenchmarkResult BenchmarkFullChain(
        int framesPerChannel, int hrirLength, bool hrtfEnabled)
    {
        var sampleRate = DefaultSampleRate;
        var bufferLength = framesPerChannel * 2;
        var scenario = hrtfEnabled
            ? $"Full Chain {hrirLength} taps"
            : "Full Chain Disabled";

        var service = new GamingEnhancementService();
        service.SetSampleRate(sampleRate);

        // Apply a gaming profile so all chain effects are active
        var gamingProfile = GamingProfilePresets.GetById("gaming-default")
                            ?? GamingProfilePresets.Profiles.FirstOrDefault();
        if (gamingProfile is not null)
            service.Apply(gamingProfile);

        if (hrtfEnabled && hrirLength > 0)
        {
            var hrtfProfile = CreateSyntheticProfile(hrirLength);
            service.ApplyHrtfProfile(hrtfProfile);
        }
        else
        {
            service.HrtfSpatializer.IsEnabled = false;
        }

        var input = CreateDeterministicInput(bufferLength);

        return RunBenchmark(scenario, sampleRate, framesPerChannel, hrirLength,
            null, input, chain: service.Chain);
    }

    // ── Core Benchmark Runner ──────────────────────────────────────────

    private static HrtfBenchmarkResult RunBenchmark(
        string scenario, int sampleRate, int framesPerChannel, int hrirLength,
        HrtfEffect? hrtf, float[] input,
        DSPChain? chain = null)
    {
        var bufferLength = input.Length;

        // Warm up — JIT, cache lines, branch predictors
        var warmupBuf = new float[bufferLength];
        for (int i = 0; i < DefaultWarmupIterations; i++)
        {
            input.AsSpan().CopyTo(warmupBuf);
            if (chain is not null)
                chain.Process(warmupBuf);
            else
                hrtf!.Process(warmupBuf);
        }

        // Normalize GC state before allocation measurement
        GC.Collect(0, GCCollectionMode.Forced, false);
        GC.WaitForPendingFinalizers();
        GC.Collect(0, GCCollectionMode.Forced, false);

        var gen0Before = GC.CollectionCount(0);
        var gen1Before = GC.CollectionCount(1);
        var gen2Before = GC.CollectionCount(2);
        var allocBefore = GC.GetAllocatedBytesForCurrentThread();

        var timings = new double[DefaultMeasuredIterations];
        var processBuf = new float[bufferLength];
        var sw = new Stopwatch();

        for (int i = 0; i < DefaultMeasuredIterations; i++)
        {
            input.AsSpan().CopyTo(processBuf);

            sw.Restart();
            if (chain is not null)
                chain.Process(processBuf);
            else
                hrtf!.Process(processBuf);
            sw.Stop();

            timings[i] = sw.Elapsed.TotalMicroseconds;
        }

        var allocAfter = GC.GetAllocatedBytesForCurrentThread();
        var gen0After = GC.CollectionCount(0);
        var gen1After = GC.CollectionCount(1);
        var gen2After = GC.CollectionCount(2);

        Array.Sort(timings);

        var availableUs = (double)framesPerChannel / sampleRate * 1_000_000.0;

        var totalAlloc = allocAfter - allocBefore;
        var bytesPerBlock = (double)totalAlloc / DefaultMeasuredIterations;

        return new HrtfBenchmarkResult
        {
            Scenario = scenario,
            SampleRate = sampleRate,
            FramesPerChannel = framesPerChannel,
            HrirLength = hrirLength,
            Iterations = DefaultMeasuredIterations,
            MinMicroseconds = timings[0],
            MedianMicroseconds = timings[DefaultMeasuredIterations / 2],
            AverageMicroseconds = timings.Average(),
            P95Microseconds = timings[(int)(DefaultMeasuredIterations * 0.95)],
            P99Microseconds = timings[(int)(DefaultMeasuredIterations * 0.99)],
            MaxMicroseconds = timings[^1],
            AvailableAudioTimeMicroseconds = availableUs,
            AverageBudgetUsagePercent = timings.Average() / availableUs * 100.0,
            P95BudgetUsagePercent = timings[(int)(DefaultMeasuredIterations * 0.95)] / availableUs * 100.0,
            P99BudgetUsagePercent = timings[(int)(DefaultMeasuredIterations * 0.99)] / availableUs * 100.0,
            WorstCaseBudgetUsagePercent = timings[^1] / availableUs * 100.0,
            TotalAllocatedBytes = totalAlloc,
            BytesPerBlock = bytesPerBlock,
            Gen0Collections = gen0After - gen0Before,
            Gen1Collections = gen1After - gen1Before,
            Gen2Collections = gen2After - gen2Before,
        };
    }

    // ── Helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a synthetic HRTF profile with the given HRIR length.
    /// HRIR data is deterministic decaying noise — not real measurements.
    /// </summary>
    public static HrtfProfile CreateSyntheticProfile(int irLength)
    {
        var leftEar = GenerateDecayingHrir(irLength, seed: 42);
        var rightEar = GenerateDecayingHrir(irLength, seed: 137);

        return new HrtfProfile
        {
            Id = $"bench-{irLength}",
            Name = $"Benchmark {irLength} taps",
            Description = $"SYNTHETIC benchmark profile with {irLength}-tap HRIR. Not real measurements.",
            SampleRate = DefaultSampleRate,
            IrLength = irLength,
            DataSource = "SYNTHETIC - Benchmark Only",
            Entries =
            [
                new HrtfEntry
                {
                    AzimuthDeg = 0,
                    ElevationDeg = 0,
                    LeftEarResponse = leftEar,
                    RightEarResponse = rightEar
                }
            ]
        };
    }

    private static float[] GenerateDecayingHrir(int length, int seed)
    {
        var rng = new Random(seed);
        var hrir = new float[length];
        for (int i = 0; i < length; i++)
        {
            var decay = Math.Exp(-3.0 * i / length);
            hrir[i] = (float)(decay * (rng.NextDouble() * 2.0 - 1.0));
        }
        return hrir;
    }

    /// <summary>
    /// Creates deterministic stereo interleaved input: left=sine, right=sine+transient.
    /// </summary>
    private static float[] CreateDeterministicInput(int bufferLength)
    {
        var input = new float[bufferLength];
        var frames = bufferLength / 2;

        for (int i = 0; i < frames; i++)
        {
            var t = (double)i / DefaultSampleRate;
            var sine = (float)(0.5 * Math.Sin(2 * Math.PI * 440.0 * t));

            // Add transient every 480 samples
            var transient = (i % 480 == 0) ? 0.3f : 0f;

            input[i * 2] = sine;
            input[i * 2 + 1] = sine * 0.7f + transient;
        }

        return input;
    }
}
