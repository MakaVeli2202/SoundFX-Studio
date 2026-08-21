using NAudio.Wave;
using SoundFXStudio.Services.Diagnostics;

namespace SoundFXStudio.Services.DSP;

/// <summary>
/// Wraps an ISampleProvider with DSP processing via DSPChain.
/// Optionally instruments DSP processing time via AudioProcessingMonitor.
///
/// Thread safety: The monitor hook is set from the UI thread before playback
/// starts and read from the audio thread. This is safe because:
/// - The reference is set once before the audio thread starts reading
/// - No locks are taken on the audio path
/// - Volatile semantics are provided by the reference assignment
/// </summary>
public sealed class EffectSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly DSPChain _chain;
    private volatile AudioProcessingMonitor? _monitor;
    private volatile AudioBlockTimestampMonitor? _timestampMonitor;

    public EffectSampleProvider(ISampleProvider source, DSPChain chain)
    {
        _source = source;
        _chain = chain;
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>
    /// Optional monitoring hook. Set before playback starts, cleared on disposal.
    /// When set, each Read() call measures DSP processing time.
    /// When null, zero-overhead — just a volatile read per block.
    /// </summary>
    public AudioProcessingMonitor? Monitor
    {
        get => _monitor;
        set => _monitor = value;
    }

    /// <summary>
    /// Optional timestamp monitoring hook for block timing analysis.
    /// Set before playback starts, cleared on disposal.
    /// When null, zero-overhead — just a volatile read per block.
    /// </summary>
    public AudioBlockTimestampMonitor? TimestampMonitor
    {
        get => _timestampMonitor;
        set => _timestampMonitor = value;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var read = _source.Read(buffer, offset, count);

        var monitor = _monitor;
        var tsMonitor = _timestampMonitor;

        if (monitor is not null || tsMonitor is not null)
        {
            tsMonitor?.RecordDspEntry();
            monitor?.StartTiming();
            _chain.Process(buffer.AsSpan(offset, read));
            monitor?.StopTiming();
            tsMonitor?.RecordDspExit();
        }
        else
        {
            _chain.Process(buffer.AsSpan(offset, read));
        }

        tsMonitor?.RecordOutputSubmit();

        return read;
    }
}
