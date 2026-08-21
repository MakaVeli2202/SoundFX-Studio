namespace SoundFXStudio.Tests.Performance;

/// <summary>
/// Structured result of a single HRTF or DSP chain benchmark scenario.
/// All timing values are in microseconds (µs).
/// Block-size convention: FramesPerChannel (mono frame count).
/// Interleaved buffer length = FramesPerChannel × 2 (stereo).
/// </summary>
public sealed class HrtfBenchmarkResult
{
    public string Scenario { get; init; } = string.Empty;
    public int SampleRate { get; init; }
    public int FramesPerChannel { get; init; }
    public int InterleavedBufferLength => FramesPerChannel * 2;
    public int HrirLength { get; init; }
    public int Iterations { get; init; }

    public double MinMicroseconds { get; init; }
    public double MedianMicroseconds { get; init; }
    public double AverageMicroseconds { get; init; }
    public double P95Microseconds { get; init; }
    public double P99Microseconds { get; init; }
    public double MaxMicroseconds { get; init; }

    /// <summary>Available audio time for this block size in µs (FramesPerChannel / SampleRate × 1e6).</summary>
    public double AvailableAudioTimeMicroseconds { get; init; }

    public double AverageBudgetUsagePercent { get; init; }
    public double P95BudgetUsagePercent { get; init; }
    public double P99BudgetUsagePercent { get; init; }
    public double WorstCaseBudgetUsagePercent { get; init; }

    public long TotalAllocatedBytes { get; init; }
    public double BytesPerBlock { get; init; }

    public int Gen0Collections { get; init; }
    public int Gen1Collections { get; init; }
    public int Gen2Collections { get; init; }

    public string BudgetClassification => WorstCaseBudgetUsagePercent switch
    {
        < 50.0 => "Comfortable",
        < 80.0 => "Monitor",
        _ => "At Risk"
    };

    public override string ToString()
        => $"{Scenario} | {FramesPerChannel} frames | HRIR {HrirLength} | " +
           $"Avg {AverageMicroseconds:F1} µs | P95 {P95Microseconds:F1} µs | " +
           $"P99 {P99Microseconds:F1} µs | Max {MaxMicroseconds:F1} µs | " +
           $"Budget P99 {P99BudgetUsagePercent:F2}% | {BudgetClassification}";
}
