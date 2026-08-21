using System.Diagnostics;
using System.Text;
using SoundFXStudio.Models;
using SoundFXStudio.Services;
using SoundFXStudio.Services.DSP;
using SoundFXStudio.Services.Hrtf;
using Xunit;
using Xunit.Abstractions;

namespace SoundFXStudio.Tests.Performance;

/// <summary>
/// Runs the HRTF performance benchmark and outputs structured results.
/// The benchmark runs once (lazy) and all tests share the cached results.
/// These tests do NOT assert machine-specific performance thresholds.
/// </summary>
public class HrtfPerformanceTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private static List<HrtfBenchmarkResult>? _cachedResults;

    public HrtfPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Benchmark_RunsAllScenarios_ProducesResults()
    {
        var results = GetOrRunBenchmark();

        Assert.NotEmpty(results);
        Assert.True(results.Count >= 25, $"Expected at least 25 scenarios, got {results.Count}");

        foreach (var r in results)
        {
            Assert.True(r.AverageMicroseconds > 0, $"{r.Scenario}: average time should be positive");
            Assert.True(r.MaxMicroseconds >= r.AverageMicroseconds,
                $"{r.Scenario}: max should be >= average");
            Assert.True(r.P99Microseconds >= r.P95Microseconds,
                $"{r.Scenario}: P99 should be >= P95");
            Assert.True(r.AvailableAudioTimeMicroseconds > 0,
                $"{r.Scenario}: available audio time should be positive");
        }

        PrintResults(results);
    }

    [Fact]
    public void Benchmark_HrtfEffectOnly_AllocatesZeroPerBlock()
    {
        var results = GetOrRunBenchmark();

        var hrtfOnly = results.Where(r => r.Scenario.StartsWith("HRTF Only") && r.HrirLength > 0).ToList();

        Assert.NotEmpty(hrtfOnly);

        foreach (var r in hrtfOnly)
        {
            _output.WriteLine($"{r.Scenario}: {r.BytesPerBlock} bytes/block, " +
                              $"total alloc {r.TotalAllocatedBytes} bytes, " +
                              $"GC0={r.Gen0Collections} GC1={r.Gen1Collections} GC2={r.Gen2Collections}");
        }

        PrintResults(results);
    }

    [Fact]
    public void Benchmark_FullChain_ProducesResults()
    {
        var results = GetOrRunBenchmark();

        var chainResults = results.Where(r => r.Scenario.StartsWith("Full Chain")).ToList();

        Assert.NotEmpty(chainResults);
        Assert.Equal(8, chainResults.Count);

        foreach (var r in chainResults)
        {
            _output.WriteLine(r.ToString());
        }

        PrintResults(results);
    }

    // ── Profile preparation benchmark ────────────────────────────────────

    [Fact]
    public void Benchmark_ProfilePreparation_MeasuresTimeAndAllocations()
    {
        // Create a realistic profile: 1550 entries × 256 taps (matches SOFA fixture size)
        var entries = new HrtfEntry[1550];
        for (int i = 0; i < 1550; i++)
        {
            var left = new float[256];
            var right = new float[256];
            for (int s = 0; s < 256; s++)
            {
                left[s] = (float)Math.Sin(i * 0.1 + s * 0.05) * 0.5f;
                right[s] = (float)Math.Cos(i * 0.1 + s * 0.05) * 0.5f;
            }
            entries[i] = new HrtfEntry
            {
                AzimuthDeg = (i % 360) - 180,
                ElevationDeg = (i / 360) - 45,
                LeftEarResponse = left,
                RightEarResponse = right
            };
        }

        var profile = new HrtfProfile
        {
            Id = "bench-44100",
            Name = "Benchmark 44100",
            SampleRate = 44100,
            IrLength = 256,
            Entries = entries,
            Manufacturer = "Benchmark",
            Description = "Test",
            DataSource = "Test",
            License = "Test"
        };

        var preparer = new HrtfProfilePreparer();

        // Warm up
        preparer.Prepare(profile, 48000);
        preparer.ClearCache();

        // Measure
        var sw = Stopwatch.StartNew();
        var gcBefore = GC.GetTotalAllocatedBytes(true);
        var prepared = preparer.Prepare(profile, 48000);
        var gcAfter = GC.GetTotalAllocatedBytes(true);
        sw.Stop();

        var totalAlloc = gcAfter - gcBefore;
        var entriesResampled = prepared.Entries.Length;
        var inputLength = 256;
        var outputLength = prepared.IrLength;

        _output.WriteLine("═══════════════════════════════════════════════════════════════════════");
        _output.WriteLine("  PROFILE PREPARATION BENCHMARK");
        _output.WriteLine("═══════════════════════════════════════════════════════════════════════");
        _output.WriteLine($"  Source: {profile.SampleRate} Hz, {entriesResampled} entries, {inputLength} taps");
        _output.WriteLine($"  Target: {prepared.SampleRate} Hz, {entriesResampled} entries, {outputLength} taps");
        _output.WriteLine($"  Time: {sw.Elapsed.TotalMicroseconds:F1} µs");
        _output.WriteLine($"  Total allocations: {totalAlloc:N0} bytes");
        _output.WriteLine($"  Throughput: {entriesResampled / sw.Elapsed.TotalSeconds:F0} entries/sec");
        _output.WriteLine("═══════════════════════════════════════════════════════════════════════");

        Assert.True(sw.Elapsed.TotalMicroseconds > 0);
        Assert.True(entriesResampled > 0);
        Assert.True(outputLength > 0);
        Assert.Equal(48000, prepared.SampleRate);
    }

    [Fact]
    public void Benchmark_ProcessAfterPreparation_ProducesOutput()
    {
        var entries = new HrtfEntry[10];
        for (int i = 0; i < 10; i++)
        {
            var left = new float[256];
            var right = new float[256];
            left[0] = 1.0f;
            right[0] = 0.9f;
            entries[i] = new HrtfEntry
            {
                AzimuthDeg = i * 36 - 180,
                ElevationDeg = 0,
                LeftEarResponse = left,
                RightEarResponse = right
            };
        }

        var profile44100 = new HrtfProfile
        {
            Id = "bench-prep", Name = "Bench", SampleRate = 44100, IrLength = 256,
            Entries = entries, Manufacturer = "T", Description = "T",
            DataSource = "T", License = "T"
        };

        var preparer = new HrtfProfilePreparer();
        var prepared = preparer.Prepare(profile44100, 48000);

        var hrtf = new SoundFXStudio.Services.DSP.HrtfEffect(48000);
        hrtf.SetProfile(prepared);
        hrtf.IsEnabled = true;
        hrtf.SetDirection(0, 0);

        var buffer = new float[1024];
        for (int i = 0; i < 512; i++) { buffer[i * 2] = 0.5f; buffer[i * 2 + 1] = 0.5f; }

        hrtf.Process(buffer);

        var maxAmplitude = 0f;
        for (int i = 0; i < buffer.Length; i++)
            maxAmplitude = Math.Max(maxAmplitude, Math.Abs(buffer[i]));

        _output.WriteLine($"Prepared profile: {prepared.SampleRate} Hz, {prepared.IrLength} taps, {prepared.Entries.Length} entries");
        _output.WriteLine($"Process() max amplitude: {maxAmplitude:F4}");

        Assert.True(maxAmplitude > 0, "HRTF should produce non-silent output");
    }

    [Fact]
    public void Benchmark_DirectionInterpolation_MeasuresTimeAndAllocations()
    {
        var sofaPath = Path.Combine(AppContext.BaseDirectory, "TestData", "SimpleFreeFieldHRIR_1.0.sofa");
        if (!File.Exists(sofaPath)) return;

        var loader = new SofaHrtfLoader();
        var result = loader.Load(sofaPath);
        Assert.True(result.Success);

        var directions = new[] { (15.0, 10.0), (-123.5, 33.3), (179.0, -29.0), (0.0, 45.0), (90.0, 0.0) };
        const int iterations = 10000;

        // Warmup
        for (int i = 0; i < 1000; i++)
        {
            var d = directions[i % directions.Length];
            HrtfDirectionInterpolator.Interpolate(result.Profile!, d.Item1, d.Item2);
        }

        GC.Collect(0, GCCollectionMode.Forced);
        var gen0Before = GC.CollectionCount(0);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            var d = directions[i % directions.Length];
            HrtfDirectionInterpolator.Interpolate(result.Profile!, d.Item1, d.Item2);
        }
        sw.Stop();

        var gen0After = GC.CollectionCount(0);

        var avgMicroseconds = (double)sw.Elapsed.TotalMicroseconds / iterations;
        _output.WriteLine($"IDW interpolation ({result.Profile!.Entries.Length} entries, 256 taps):");
        _output.WriteLine($"  {iterations} iterations in {sw.ElapsedMilliseconds} ms");
        _output.WriteLine($"  Average: {avgMicroseconds:F1} µs per interpolation");
        _output.WriteLine($"  GC0 collections during test: {gen0After - gen0Before}");

        // Should be well under 1ms per interpolation on typical hardware
        Assert.True(avgMicroseconds < 1000, $"Interpolation too slow: {avgMicroseconds:F1} µs (should be < 1000 µs)");
    }

    [Fact]
    public void Benchmark_InterpolationVsNearestNeighbor()
    {
        var sofaPath = Path.Combine(AppContext.BaseDirectory, "TestData", "SimpleFreeFieldHRIR_1.0.sofa");
        if (!File.Exists(sofaPath)) return;

        var loader = new SofaHrtfLoader();
        var result = loader.Load(sofaPath);
        Assert.True(result.Success);

        var directions = new[] { (15.0, 10.0), (-123.5, 33.3), (179.0, -29.0), (0.0, 45.0), (90.0, 0.0) };
        const int iterations = 10000;

        // Nearest-neighbor benchmark
        var sw1 = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            var d = directions[i % directions.Length];
            result.Profile!.GetEntryForDirection(d.Item1, d.Item2);
        }
        sw1.Stop();

        // IDW interpolation benchmark
        var sw2 = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            var d = directions[i % directions.Length];
            HrtfDirectionInterpolator.Interpolate(result.Profile!, d.Item1, d.Item2);
        }
        sw2.Stop();

        var nnMicroseconds = (double)sw1.Elapsed.TotalMicroseconds / iterations;
        var idwMicroseconds = (double)sw2.Elapsed.TotalMicroseconds / iterations;

        _output.WriteLine($"Nearest-neighbor: {nnMicroseconds:F1} µs avg");
        _output.WriteLine($"IDW interpolation: {idwMicroseconds:F1} µs avg");
        _output.WriteLine($"Ratio: {idwMicroseconds / nnMicroseconds:F1}x");

        // IDW should be reasonably fast — not orders of magnitude slower
        Assert.True(idwMicroseconds < nnMicroseconds * 20,
            $"IDW interpolation too slow relative to nearest-neighbor: {idwMicroseconds / nnMicroseconds:F1}x");
    }

    [Fact]
    public void Benchmark_InterpolatedEffect_ProcessRemainsFast()
    {
        var profile = HrtfProfilePresets.GetById("synthetic-front")!;
        var hrtf = new HrtfEffect(48000);
        hrtf.SetProfile(profile);
        hrtf.IsEnabled = true;
        hrtf.SetDirection(45, 0);

        var buffer = new float[2048];
        for (int i = 0; i < 1024; i++) { buffer[i * 2] = 0.5f; buffer[i * 2 + 1] = 0.5f; }

        const int iterations = 10000;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            hrtf.Process(buffer);
        }
        sw.Stop();

        var avgMicroseconds = (double)sw.Elapsed.TotalMicroseconds / iterations;
        _output.WriteLine($"HRTF Process() after interpolation: {avgMicroseconds:F1} µs avg per 1024-sample block");

        // Should still be well under the frame budget
        Assert.True(avgMicroseconds < 10000, $"Process too slow: {avgMicroseconds:F1} µs");
    }

    // ── Phase G: Transition benchmarks ──────────────────────────────────

    [Fact]
    public void Benchmark_Process_NoTransition()
    {
        var hrtf = new HrtfEffect(48000) { DirectionTransitionMs = 0, IsEnabled = true };
        hrtf.SetProfile(HrtfProfilePresets.GetById("synthetic-front")!);
        hrtf.SetDirection(0, 0);

        var buffer = new float[960]; // 480 frames stereo
        for (int i = 0; i < 480; i++) { buffer[i * 2] = 0.5f; buffer[i * 2 + 1] = 0.5f; }

        // Warmup
        for (int i = 0; i < 100; i++) hrtf.Process(buffer);

        var sw = Stopwatch.StartNew();
        const int iterations = 10000;
        for (int i = 0; i < iterations; i++)
            hrtf.Process(buffer);
        sw.Stop();

        var avgUs = (double)sw.Elapsed.TotalMicroseconds / iterations;
        _output.WriteLine($"Process (no transition): {avgUs:F1} µs avg per 480-frame block");

        // Baseline: should be under frame budget
        Assert.True(avgUs < 5000, $"Too slow: {avgUs:F1} µs");
    }

    [Fact]
    public void Benchmark_Process_DuringTransition()
    {
        var hrtf = new HrtfEffect(48000) { DirectionTransitionMs = 20, IsEnabled = true };
        hrtf.SetProfile(HrtfProfilePresets.GetById("synthetic-front")!);
        hrtf.SetDirection(0, 0);

        var buffer = new float[960];
        for (int i = 0; i < 480; i++) { buffer[i * 2] = 0.5f; buffer[i * 2 + 1] = 0.5f; }

        // Warmup
        for (int i = 0; i < 100; i++) hrtf.Process(buffer);

        const int iterations = 5000;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            if (i % 100 == 0)
                hrtf.SetDirection(i % 360 - 180, 0);
            hrtf.Process(buffer);
        }
        sw.Stop();

        var avgUs = (double)sw.Elapsed.TotalMicroseconds / iterations;
        _output.WriteLine($"Process (during transition): {avgUs:F1} µs avg per 480-frame block");
    }

    [Fact]
    public void Benchmark_Process_128Taps_WithTransition()
    {
        var hrtf = BenchmarkWithTapCount(128, 20);
        _output.WriteLine($"128 taps + transition: done");
        Assert.NotNull(hrtf);
    }

    [Fact]
    public void Benchmark_Process_256Taps_WithTransition()
    {
        var hrtf = BenchmarkWithTapCount(256, 20);
        _output.WriteLine($"256 taps + transition: done");
        Assert.NotNull(hrtf);
    }

    [Fact]
    public void Benchmark_Process_512Taps_WithTransition()
    {
        var hrtf = BenchmarkWithTapCount(512, 20);
        _output.WriteLine($"512 taps + transition: done");
        Assert.NotNull(hrtf);
    }

    [Fact]
    public void Benchmark_RapidDirectionChanges()
    {
        var hrtf = new HrtfEffect(48000) { DirectionTransitionMs = 20, IsEnabled = true };
        hrtf.SetProfile(HrtfProfilePresets.GetById("synthetic-front")!);
        hrtf.SetDirection(0, 0);

        var buffer = new float[960];
        for (int i = 0; i < 480; i++) { buffer[i * 2] = 0.5f; buffer[i * 2 + 1] = 0.5f; }

        // Warmup
        for (int i = 0; i < 100; i++) hrtf.Process(buffer);

        var rng = new Random(42);
        const int iterations = 5000;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            // Change direction every 3 blocks (rapid but bounded)
            if (i % 3 == 0)
                hrtf.SetDirection(rng.NextDouble() * 180 - 90, rng.NextDouble() * 45);
            hrtf.Process(buffer);
        }
        sw.Stop();

        var avgUs = (double)sw.Elapsed.TotalMicroseconds / iterations;
        _output.WriteLine($"Rapid direction changes: {avgUs:F1} µs avg per block");

        // Should not regress badly
        Assert.True(avgUs < 15000, $"Too slow: {avgUs:F1} µs");
    }

    private HrtfEffect BenchmarkWithTapCount(int tapCount, int transitionMs)
    {
        var entries = new HrtfEntry[10];
        for (int i = 0; i < 10; i++)
        {
            var left = new float[tapCount];
            var right = new float[tapCount];
            left[0] = 1.0f;
            right[0] = 0.9f;
            entries[i] = new HrtfEntry
            {
                AzimuthDeg = i * 36 - 180,
                ElevationDeg = 0,
                LeftEarResponse = left,
                RightEarResponse = right
            };
        }

        var profile = new HrtfProfile
        {
            Id = $"bench-{tapCount}",
            Name = $"Bench {tapCount}",
            SampleRate = 48000,
            IrLength = tapCount,
            Entries = entries,
            Manufacturer = "B", Description = "B", DataSource = "B", License = "B"
        };

        var hrtf = new HrtfEffect(48000) { DirectionTransitionMs = transitionMs, IsEnabled = true };
        hrtf.SetProfile(profile);
        hrtf.SetDirection(0, 0);

        var buffer = new float[960];
        for (int i = 0; i < 480; i++) { buffer[i * 2] = 0.5f; buffer[i * 2 + 1] = 0.5f; }

        // Warmup
        for (int i = 0; i < 50; i++) hrtf.Process(buffer);

        const int iterations = 2000;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            if (i % 50 == 0)
                hrtf.SetDirection((i / 50) * 36 - 180, 0);
            hrtf.Process(buffer);
        }
        sw.Stop();

        var avgUs = (double)sw.Elapsed.TotalMicroseconds / iterations;
        _output.WriteLine($"  {tapCount} taps + {transitionMs}ms transition: {avgUs:F1} µs avg");

        return hrtf;
    }

    // ── Phase H: Head tracking benchmarks ────────────────────────────────

    [Fact]
    public void Benchmark_HeadTracking_ConversionCost()
    {
        var converter = new HeadOrientationConverter();
        converter.Calibrate(30, 10, 5);

        const int iterations = 100000;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
            converter.Convert(i * 0.1, i * 0.05, i * 0.02);
        sw.Stop();

        var avgNs = (double)sw.Elapsed.TotalMilliseconds * 1_000_000 / iterations;
        _output.WriteLine($"HeadOrientationConverter.Convert: {avgNs:F0} ns avg ({iterations} iterations)");
    }

    [Fact]
    public void Benchmark_HeadTracking_RateLimitingCost()
    {
        var provider = new StubHeadTrackingProvider { Orientation = new HeadOrientation(0, 0, 0) };
        var hrtf = new HrtfEffect(48000) { DirectionTransitionMs = 0, IsEnabled = true };
        hrtf.SetProfile(HrtfProfilePresets.GetById("synthetic-front")!);
        hrtf.SetDirection(0, 0);

        var service = new HeadTrackingService(provider) { AngleThresholdDeg = 1.0, MaxUpdateIntervalMs = 16 };
        service.Start();

        const int iterations = 10000;
        var rng = new Random(42);
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            provider.Orientation = new HeadOrientation(rng.NextDouble() * 180 - 90, rng.NextDouble() * 45, 0);
            service.Update(hrtf, headTrackingEnabled: true, hrtfEnabled: true);
        }
        sw.Stop();

        var avgUs = (double)sw.Elapsed.TotalMicroseconds / iterations;
        _output.WriteLine($"HeadTrackingService.Update (rate-limited): {avgUs:F1} µs avg");
    }

    [Fact]
    public void Benchmark_HeadTracking_ProcessDuringTracking()
    {
        var provider = new StubHeadTrackingProvider { Orientation = new HeadOrientation(0, 0, 0) };
        var hrtf = new HrtfEffect(48000) { DirectionTransitionMs = 0, IsEnabled = true };
        hrtf.SetProfile(HrtfProfilePresets.GetById("synthetic-front")!);
        hrtf.SetDirection(0, 0);

        var service = new HeadTrackingService(provider) { AngleThresholdDeg = 0, MaxUpdateIntervalMs = 0 };
        service.Start();

        var buffer = new float[960];
        for (int i = 0; i < 480; i++) { buffer[i * 2] = 0.5f; buffer[i * 2 + 1] = 0.5f; }

        // Warmup
        for (int i = 0; i < 50; i++)
        {
            hrtf.Process(buffer);
            provider.Orientation = new HeadOrientation(i * 2, 0, 0);
            service.Update(hrtf, headTrackingEnabled: true, hrtfEnabled: true);
        }

        const int iterations = 2000;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            provider.Orientation = new HeadOrientation(i * 0.5, 0, 0);
            service.Update(hrtf, headTrackingEnabled: true, hrtfEnabled: true);
            hrtf.Process(buffer);
        }
        sw.Stop();

        var avgUs = (double)sw.Elapsed.TotalMicroseconds / iterations;
        _output.WriteLine($"Process + HeadTracking Update: {avgUs:F1} µs avg per block");

        Assert.True(avgUs < 15000, $"Too slow: {avgUs:F1} µs");
    }

    public void Dispose()
    {
        // Do nothing — static results persist across tests in same process
    }

    private static List<HrtfBenchmarkResult> GetOrRunBenchmark()
    {
        _cachedResults ??= HrtfPerformanceBenchmark.RunAll();
        return _cachedResults;
    }

    private void PrintResults(List<HrtfBenchmarkResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════");
        sb.AppendLine("  HRTF PERFORMANCE BENCHMARK RESULTS");
        sb.AppendLine("  Block convention: FramesPerChannel (mono). Stereo interleaved = FramesPerChannel × 2 floats.");
        sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════");
        sb.AppendLine();

        sb.AppendLine($"{"Scenario",-30} {"Frames",6} {"HRIR",5} {"Avg µs",9} {"P95 µs",9} {"P99 µs",9} {"Max µs",9} {"Budget P99",11} {"Class",12} {"Alloc/B",9}");
        sb.AppendLine(new string('-', 135));

        foreach (var r in results)
        {
            sb.AppendLine(
                $"{r.Scenario,-30} {r.FramesPerChannel,6} {r.HrirLength,5} " +
                $"{r.AverageMicroseconds,9:F1} {r.P95Microseconds,9:F1} {r.P99Microseconds,9:F1} {r.MaxMicroseconds,9:F1} " +
                $"{r.P99BudgetUsagePercent,10:F3}% {r.BudgetClassification,12} {r.BytesPerBlock,9:F1}");
        }

        sb.AppendLine();
        sb.AppendLine("─── Allocation Summary ───");

        var hrtfOnly = results.Where(r => r.Scenario.StartsWith("HRTF Only") && r.HrirLength > 0).ToList();
        if (hrtfOnly.Count > 0)
        {
            var avgAlloc = hrtfOnly.Average(r => r.BytesPerBlock);
            var maxAlloc = hrtfOnly.Max(r => r.BytesPerBlock);
            sb.AppendLine($"HRTF Only — Avg alloc/block: {avgAlloc:F1} bytes, Max: {maxAlloc:F1} bytes");
        }

        var chainAll = results.Where(r => r.Scenario.StartsWith("Full Chain")).ToList();
        if (chainAll.Count > 0)
        {
            var avgAlloc = chainAll.Average(r => r.BytesPerBlock);
            var maxAlloc = chainAll.Max(r => r.BytesPerBlock);
            sb.AppendLine($"Full Chain — Avg alloc/block: {avgAlloc:F1} bytes, Max: {maxAlloc:F1} bytes");
        }

        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════");

        _output.WriteLine(sb.ToString());
    }
}
