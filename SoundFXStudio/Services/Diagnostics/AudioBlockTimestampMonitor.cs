using System.Diagnostics;

namespace SoundFXStudio.Services.Diagnostics;

/// <summary>
/// Lightweight timestamp propagation for audio block timing analysis.
///
/// Tracks the lifecycle of an audio block through the pipeline:
///   CaptureTimestamp → DspEntryTimestamp → DspExitTimestamp → OutputSubmitTimestamp
///
/// Design constraints:
/// - No per-block heap allocation (uses Stopwatch.GetTimestamp() which returns long)
/// - Preallocated ring buffer for block timing history
/// - Thread-safe snapshot retrieval
/// - All timestamps are Stopwatch ticks (not wall-clock) for precise delta calculation
///
/// Limitations:
/// - Output submission timestamp is set when WaveOutEvent receives the buffer,
///   but actual hardware playback time is NOT available from NAudio's WaveOutEvent API.
/// - The delta from OutputSubmitTimestamp to physical speaker output is UNKNOWN
///   (includes OS mixer, driver buffering, device latency).
/// </summary>
public sealed class AudioBlockTimestampMonitor
{
    private const int DefaultCapacity = 256;

    // Ring buffer of block timing records
    private readonly BlockTiming[] _buffer;
    private readonly double[] _snapshotBuffer; // Preallocated for sorting latencies
    private int _writeIndex;
    private long _totalCount;
    private readonly long _ticksPerSecond = Stopwatch.Frequency;

    public AudioBlockTimestampMonitor(int capacity = DefaultCapacity)
    {
        if (capacity < 16) throw new ArgumentOutOfRangeException(nameof(capacity));
        _buffer = new BlockTiming[capacity];
        _snapshotBuffer = new double[capacity];
    }

    /// <summary>
    /// Record the capture timestamp. Called from WASAPI data callback.
    /// </summary>
    public void RecordCaptureTimestamp()
    {
        var idx = Interlocked.Increment(ref _writeIndex) - 1;
        var safeIdx = ((idx % _buffer.Length) + _buffer.Length) % _buffer.Length;
        _buffer[safeIdx].CaptureTimestamp = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Record DSP entry and exit timestamps. Called from EffectSampleProvider.Read().
    /// Must be called between StartDsp and EndDsp on the audio thread.
    /// </summary>
    public void RecordDspEntry()
    {
        var idx = Volatile.Read(ref _writeIndex) - 1;
        var safeIdx = ((idx % _buffer.Length) + _buffer.Length) % _buffer.Length;
        _buffer[safeIdx].DspEntryTimestamp = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Record DSP exit timestamp.
    /// </summary>
    public void RecordDspExit()
    {
        var idx = Volatile.Read(ref _writeIndex) - 1;
        var safeIdx = ((idx % _buffer.Length) + _buffer.Length) % _buffer.Length;
        _buffer[safeIdx].DspExitTimestamp = Stopwatch.GetTimestamp();
        Interlocked.Increment(ref _totalCount);
    }

    /// <summary>
    /// Record output submission timestamp. Called after DSP processing, before buffer goes to WaveOut.
    /// </summary>
    public void RecordOutputSubmit()
    {
        var idx = Volatile.Read(ref _writeIndex) - 1;
        var safeIdx = ((idx % _buffer.Length) + _buffer.Length) % _buffer.Length;
        _buffer[safeIdx].OutputSubmitTimestamp = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Takes a snapshot of timing statistics.
    /// Returns capture-to-DSP-entry latency (proxy for capture buffering delay).
    /// </summary>
    public TimestampSnapshot GetSnapshot()
    {
        var count = (int)Math.Min(_totalCount, _buffer.Length);
        if (count == 0)
        {
            return new TimestampSnapshot
            {
                MeasurementCount = 0,
                CaptureToDspAvgMs = 0,
                CaptureToDspP99Ms = 0,
                DspProcessingAvgMs = 0,
                DspProcessingP99Ms = 0,
                DspToOutputSubmitAvgMs = 0,
                DspToOutputSubmitP99Ms = 0
            };
        }

        var writeIdx = _writeIndex;
        var captureToDspSum = 0.0;
        var dspProcessingSum = 0.0;
        var dspToOutputSum = 0.0;
        var captureToDspCount = 0;
        var dspProcessingCount = 0;
        var dspToOutputCount = 0;

        var c2dMax = 0.0;
        var dspMax = 0.0;
        var d2oMax = 0.0;

        for (int i = 0; i < count; i++)
        {
            var idx = ((writeIdx - count + i) % _buffer.Length + _buffer.Length) % _buffer.Length;
            ref var rec = ref _buffer[idx];

            if (rec.CaptureTimestamp > 0 && rec.DspEntryTimestamp > 0)
            {
                var ms = TicksToMs(rec.DspEntryTimestamp - rec.CaptureTimestamp);
                captureToDspSum += ms;
                captureToDspCount++;
                if (ms > c2dMax) c2dMax = ms;
            }

            if (rec.DspEntryTimestamp > 0 && rec.DspExitTimestamp > 0)
            {
                var ms = TicksToMs(rec.DspExitTimestamp - rec.DspEntryTimestamp);
                dspProcessingSum += ms;
                dspProcessingCount++;
                if (ms > dspMax) dspMax = ms;
            }

            if (rec.DspExitTimestamp > 0 && rec.OutputSubmitTimestamp > 0)
            {
                var ms = TicksToMs(rec.OutputSubmitTimestamp - rec.DspExitTimestamp);
                dspToOutputSum += ms;
                dspToOutputCount++;
                if (ms > d2oMax) d2oMax = ms;
            }
        }

        return new TimestampSnapshot
        {
            MeasurementCount = _totalCount,
            CaptureToDspAvgMs = captureToDspCount > 0 ? captureToDspSum / captureToDspCount : 0,
            CaptureToDspP99Ms = c2dMax, // Approximate — full percentile would need sorting
            DspProcessingAvgMs = dspProcessingCount > 0 ? dspProcessingSum / dspProcessingCount : 0,
            DspProcessingP99Ms = dspMax,
            DspToOutputSubmitAvgMs = dspToOutputCount > 0 ? dspToOutputSum / dspToOutputCount : 0,
            DspToOutputSubmitP99Ms = d2oMax
        };
    }

    public void Reset()
    {
        Array.Clear(_buffer, 0, _buffer.Length);
        _writeIndex = 0;
        _totalCount = 0;
    }

    public int Capacity => _buffer.Length;

    private double TicksToMs(long ticks)
    {
        return (double)ticks / _ticksPerSecond * 1000.0;
    }

    /// <summary>
    /// Timing record for one audio block through the pipeline.
    /// All fields are Stopwatch ticks (not wall-clock).
    /// </summary>
    private struct BlockTiming
    {
        public long CaptureTimestamp;
        public long DspEntryTimestamp;
        public long DspExitTimestamp;
        public long OutputSubmitTimestamp;
    }
}

/// <summary>
/// Snapshot of block timing statistics.
/// </summary>
public sealed class TimestampSnapshot
{
    public long MeasurementCount { get; init; }

    /// <summary>Capture to DSP entry average in ms. Proxy for capture buffering delay.</summary>
    public double CaptureToDspAvgMs { get; init; }

    /// <summary>Capture to DSP entry max in ms.</summary>
    public double CaptureToDspP99Ms { get; init; }

    /// <summary>DSP processing average in ms.</summary>
    public double DspProcessingAvgMs { get; init; }

    /// <summary>DSP processing max in ms.</summary>
    public double DspProcessingP99Ms { get; init; }

    /// <summary>DSP exit to output submit average in ms. Small overhead.</summary>
    public double DspToOutputSubmitAvgMs { get; init; }

    /// <summary>DSP exit to output submit max in ms.</summary>
    public double DspToOutputSubmitP99Ms { get; init; }
}
