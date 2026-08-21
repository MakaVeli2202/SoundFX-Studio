using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace SoundFXStudio.Services.Diagnostics;

/// <summary>
/// Measures actual DSP execution time per audio block.
///
/// Design constraints:
/// - No allocations in the audio callback after initialization.
/// - Thread-safe snapshot retrieval (audio thread writes, UI thread reads).
/// - Bounded memory (ring buffer, never grows).
/// - No locks on the audio callback path.
/// - No LINQ in the audio callback.
/// - No UI updates from the audio thread.
///
/// Instrumentation point: EffectSampleProvider.Read() → DSPChain.Process().
/// Only measures DSP processing time. Does NOT include capture wait or output wait.
/// </summary>
public sealed class AudioProcessingMonitor
{
    private const int DefaultCapacity = 1024;

    // Ring buffer — written by audio thread, read by snapshot.
    private readonly double[] _timings;
    private readonly double[] _snapshotBuffer; // Preallocated for GetSnapshot() — avoids per-call allocation
    private int _writeIndex;
    private long _totalCount;
    private double _maxUs;

    // Preallocated Stopwatch — reused, never allocated per block.
    private readonly Stopwatch _stopwatch = new();

    // Over-budget tracking — written by audio thread, read by snapshot.
    private long _overBudgetCount;
    private double _blockDurationUs;

    /// <summary>
    /// Creates a monitor with the given ring buffer capacity.
    /// Default capacity is 1024 samples (~21 seconds at 48kHz/480 frames per block).
    /// </summary>
    public AudioProcessingMonitor(int capacity = DefaultCapacity)
    {
        if (capacity < 16) throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be >= 16.");
        _timings = new double[capacity];
        _snapshotBuffer = new double[capacity];
    }

    /// <summary>
    /// Sets the block duration in microseconds for over-budget detection.
    /// Must be called before recording begins (or when sample rate changes).
    /// </summary>
    public void SetBlockDurationUs(double blockDurationUs)
    {
        _blockDurationUs = blockDurationUs;
    }

    /// <summary>
    /// Start timing. Call immediately before DSPChain.Process().
    /// Must be paired with StopTiming() on the same thread.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void StartTiming()
    {
        _stopwatch.Restart();
    }

    /// <summary>
    /// Stop timing and record the measurement.
    /// Call immediately after DSPChain.Process().
    /// Zero-allocation hot path.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void StopTiming()
    {
        _stopwatch.Stop();
        var elapsedUs = _stopwatch.Elapsed.TotalMicroseconds;

        RecordMeasurement(elapsedUs);
    }

    /// <summary>
    /// Record a pre-measured timing value in microseconds.
    /// Use this for testing or when the caller handles timing externally.
    /// </summary>
    public void RecordMeasurement(double elapsedUs)
    {
        var idx = _writeIndex;
        _timings[idx] = elapsedUs;
        _writeIndex = (idx + 1) % _timings.Length;

        Interlocked.Increment(ref _totalCount);

        // Update max (racy but safe — we want the approximate max)
        if (elapsedUs > _maxUs)
            _maxUs = elapsedUs;

        // Over-budget detection
        if (_blockDurationUs > 0 && elapsedUs > _blockDurationUs)
            Interlocked.Increment(ref _overBudgetCount);
    }

    /// <summary>
    /// Takes a thread-safe snapshot of the current statistics.
    /// Called from the UI thread. Does NOT block the audio thread.
    /// </summary>
    public AudioProcessingSnapshot GetSnapshot()
    {
        // Copy ring buffer contents for consistent computation.
        // This is a snapshot — we accept minor race conditions with the writer
        // since we're computing approximate statistics.
        var count = (int)Math.Min(_totalCount, _timings.Length);
        if (count == 0)
        {
            return new AudioProcessingSnapshot
            {
                MeasurementCount = 0,
                AverageUs = 0,
                MaxUs = 0,
                P95Us = 0,
                P99Us = 0,
                OverBudgetBlockCount = _overBudgetCount,
                BlockDurationUs = _blockDurationUs
            };
        }

        // Copy live data to preallocated buffer for sorting
        var writeIdx = _writeIndex;
        var startIdx = (writeIdx - count + _timings.Length) % _timings.Length;
        for (int i = 0; i < count; i++)
            _snapshotBuffer[i] = _timings[(startIdx + i) % _timings.Length];

        // Sort the snapshot buffer for percentile calculation
        Array.Sort(_snapshotBuffer, 0, count);

        var sum = 0.0;
        for (int i = 0; i < count; i++)
            sum += _snapshotBuffer[i];

        return new AudioProcessingSnapshot
        {
            MeasurementCount = _totalCount,
            AverageUs = sum / count,
            MaxUs = _maxUs,
            P95Us = Percentile(_snapshotBuffer, count, 0.95),
            P99Us = Percentile(_snapshotBuffer, count, 0.99),
            OverBudgetBlockCount = _overBudgetCount,
            BlockDurationUs = _blockDurationUs
        };
    }

    /// <summary>
    /// Resets all statistics. Call when capture restarts.
    /// </summary>
    public void Reset()
    {
        Array.Clear(_timings, 0, _timings.Length);
        _writeIndex = 0;
        _totalCount = 0;
        _maxUs = 0;
        _overBudgetCount = 0;
    }

    /// <summary>
    /// The ring buffer capacity.
    /// </summary>
    public int Capacity => _timings.Length;

    /// <summary>
    /// Total number of measurements recorded (may exceed capacity due to overwrites).
    /// </summary>
    public long TotalCount => _totalCount;

    private static double Percentile(double[] sorted, int count, double p)
    {
        if (count == 0) return 0;
        var idx = (int)Math.Ceiling(p * count) - 1;
        return sorted[Math.Clamp(idx, 0, count - 1)];
    }
}

/// <summary>
/// Thread-safe snapshot of DSP processing statistics.
/// Immutable value object returned by AudioProcessingMonitor.GetSnapshot().
/// </summary>
public sealed class AudioProcessingSnapshot
{
    /// <summary>Total measurements recorded (may exceed ring buffer capacity).</summary>
    public long MeasurementCount { get; init; }

    /// <summary>Average DSP processing time in microseconds.</summary>
    public double AverageUs { get; init; }

    /// <summary>Maximum DSP processing time observed in microseconds.</summary>
    public double MaxUs { get; init; }

    /// <summary>95th percentile DSP processing time in microseconds.</summary>
    public double P95Us { get; init; }

    /// <summary>99th percentile DSP processing time in microseconds.</summary>
    public double P99Us { get; init; }

    /// <summary>Number of blocks where DSP exceeded the audio deadline.</summary>
    public long OverBudgetBlockCount { get; init; }

    /// <summary>Block duration in microseconds (the audio deadline).</summary>
    public double BlockDurationUs { get; init; }
}
