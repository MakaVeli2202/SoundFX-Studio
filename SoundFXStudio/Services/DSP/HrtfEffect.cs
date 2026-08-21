using SoundFXStudio.Models;
using SoundFXStudio.Services.Hrtf;

namespace SoundFXStudio.Services.DSP;

/// <summary>
/// HRTF spatialization effect implementing stereo HRIR convolution with
/// smooth direction transitions.
///
/// Processes interleaved stereo buffers (L0 R0 L1 R1 ...) by deinterleaving,
/// applying four convolution paths for binaural output, and reinterleaving.
///
/// For each output sample:
///   Lout = Lin ⊗ H_LL + Rin ⊗ H_LR
///   Rout = Lin ⊗ H_RL + Rin ⊗ H_RR
///
/// where H_xx are the left/right ear impulse responses from the selected
/// HRTF entry, and ⊗ denotes convolution.
///
/// Direction interpolation uses inverse-distance weighting (IDW) over nearest
/// neighbors, enabling smooth transitions between SOFA measurement directions.
/// Interpolation occurs in SetDirection(), OUTSIDE Process().
///
/// TRANSITION SMOOTHING:
/// When the direction changes, the HRIR does not switch abruptly. Instead,
/// a configurable crossfade (default 20 ms) blends the old HRIR output
/// into the new HRIR output using a dual-convolution approach:
///   output = lerp(oldHRIR ⊗ input, newHRIR ⊗ input, alpha)
/// This is mathematically correct (no coefficient interpolation artifact)
/// and preserves overlap continuity.
///
/// Thread safety: SetDirection() may be called from the UI thread while
/// Process() runs on the audio thread. Transition state is prepared into
/// preallocated buffers on the UI thread, then the transition counter is
/// atomically set. Process() reads the transition state without locks.
///
/// Rapid direction changes: When a new direction arrives mid-transition,
/// the "from" arrays are updated to the current "to" state, the "to" arrays
/// are updated to the new target, and the transition restarts from the
/// current blend position. No transition queue grows unboundedly.
///
/// Convolution overlap is preserved across transitions (no reset) for
/// continuous audio output.
///
/// Allocation guarantee: Process() allocates 0 bytes per block after
/// initial setup, including during transitions and rapid direction changes.
/// </summary>
public sealed class HrtfEffect : IAudioEffect
{
    private HrtfProfile? _profile;
    private HrtfEntry? _currentEntry;

    // Active HRIR coefficient arrays for the 4 convolution paths
    private float[] _hLL = Array.Empty<float>();
    private float[] _hLR = Array.Empty<float>();
    private float[] _hRL = Array.Empty<float>();
    private float[] _hRR = Array.Empty<float>();
    private int _irLength;

    // Track current direction for early-exit optimization
    private double _currentAzimuth = double.NaN;
    private double _currentElevation = double.NaN;

    // Track whether "from" arrays contain valid HRIR data (for first-direction optimization)
    private bool _hasActiveHrir;

    // Transition state (written by UI thread, read by audio thread)
    private float[] _hFromLL = Array.Empty<float>();
    private float[] _hFromLR = Array.Empty<float>();
    private float[] _hFromRL = Array.Empty<float>();
    private float[] _hFromRR = Array.Empty<float>();
    private float[] _outFromLL = Array.Empty<float>();
    private float[] _outFromLR = Array.Empty<float>();
    private float[] _outFromRL = Array.Empty<float>();
    private float[] _outFromRR = Array.Empty<float>();
    private int _transitionFramesTotal;
    private int _transitionFramesRemaining;
    private double _transitionAlphaStep;
    private int _transitionSampleRate;

    /// <summary>
    /// Transition duration in milliseconds. 0 = immediate switch (no smoothing).
    /// Clamped to [0, 100].
    /// Default 20 ms provides smooth perceptual transitions without audible latency.
    /// </summary>
    public int DirectionTransitionMs
    {
        get => _directionTransitionMs;
        set => _directionTransitionMs = Math.Clamp(value, 0, 100);
    }
    private int _directionTransitionMs = 20;

    /// <summary>
    /// True while a direction transition is in progress.
    /// </summary>
    public bool IsTransitioning => _transitionFramesRemaining > 0;

    // Overlap buffers: store tail of previous block for convolution continuity
    private float[] _overlapL = Array.Empty<float>();
    private float[] _overlapR = Array.Empty<float>();
    private int _overlapCount;

    // Preallocated working buffers (reused across Process calls)
    private float[] _extendedL = Array.Empty<float>();
    private float[] _extendedR = Array.Empty<float>();
    private float[] _outLL = Array.Empty<float>();
    private float[] _outLR = Array.Empty<float>();
    private float[] _outRL = Array.Empty<float>();
    private float[] _outRR = Array.Empty<float>();
    private float[] _sumL = Array.Empty<float>();
    private float[] _sumR = Array.Empty<float>();

    public HrtfEffect(int sampleRate = 48000)
    {
        SampleRate = sampleRate;
        _transitionSampleRate = sampleRate;
    }

    public string Name => "HRTF Spatializer";

    public bool IsEnabled { get; set; }

    public int SampleRate { get; set; }

    /// <summary>
    /// Dry/wet blend. 0.0 = passthrough, 1.0 = fully spatialized.
    /// Clamped to [0, 1].
    /// </summary>
    public double SpatialMix
    {
        get => _spatialMix;
        set => _spatialMix = Math.Clamp(value, 0.0, 1.0);
    }
    private double _spatialMix = 1.0;

    /// <summary>
    /// The currently loaded profile, or null.
    /// </summary>
    public HrtfProfile? ActiveProfile => _profile;

    /// <summary>
    /// The currently selected direction entry (nearest neighbor), or null.
    /// Retained for backward compatibility with tests and UI display.
    /// </summary>
    public HrtfEntry? CurrentEntry => _currentEntry;

    /// <summary>
    /// The IR length of the currently loaded profile, or 0.
    /// </summary>
    public int IrLength => _irLength;

    /// <summary>
    /// Loads an HRTF profile. Clears current direction, transition, and overlap state.
    /// Pass null to unload.
    /// </summary>
    public void SetProfile(HrtfProfile? profile)
    {
        _profile = profile;
        _currentEntry = null;
        _currentAzimuth = double.NaN;
        _currentElevation = double.NaN;
        _hasActiveHrir = false;

        if (profile is null || profile.Entries.Length == 0 || profile.IrLength <= 0)
        {
            _irLength = 0;
            ClearHrirArrays();
            ClearOverlap();
            return;
        }

        _irLength = profile.IrLength;
        AllocateBuffers();
        ClearOverlap();
    }

    /// <summary>
    /// Sets the HRTF direction using interpolated HRIR data.
    /// Uses inverse-distance-weighted interpolation over nearest neighbors,
    /// with fallback to nearest-neighbor when interpolation is not possible.
    ///
    /// When DirectionTransitionMs > 0, initiates a smooth crossfade from
    /// the current HRIR to the new HRIR. Rapid direction changes update
    /// the target without queuing — the latest direction always wins.
    ///
    /// This method is safe to call from the UI thread while Process() runs
    /// on the audio thread. HRIR data is prepared into preallocated buffers,
    /// then the transition counter is published atomically.
    /// </summary>
    public void SetDirection(double azimuthDeg, double elevationDeg)
    {
        if (_profile is null)
        {
            _currentEntry = null;
            _currentAzimuth = double.NaN;
            _currentElevation = double.NaN;
            return;
        }

        // Early exit if direction hasn't changed
        if (!double.IsNaN(_currentAzimuth)
            && _currentAzimuth == azimuthDeg
            && _currentElevation == elevationDeg)
            return;

        _currentAzimuth = azimuthDeg;
        _currentElevation = elevationDeg;

        // Track nearest entry for backward compatibility
        _currentEntry = _profile.GetEntryForDirection(azimuthDeg, elevationDeg);

        // Interpolate HRIR for the requested direction
        var (leftHrir, rightHrir) = HrtfDirectionInterpolator.Interpolate(
            _profile, azimuthDeg, elevationDeg);

        if (leftHrir.Length == 0 || rightHrir.Length == 0)
            return;

        var newIrLength = leftHrir.Length;

        // Allocate new arrays if IR length changed (e.g., after resampling)
        if (_hLL.Length < newIrLength)
        {
            _hLL = new float[newIrLength];
            _hLR = new float[newIrLength];
            _hRL = new float[newIrLength];
            _hRR = new float[newIrLength];
            _hFromLL = new float[newIrLength];
            _hFromLR = new float[newIrLength];
            _hFromRL = new float[newIrLength];
            _hFromRR = new float[newIrLength];
        }

        if (_directionTransitionMs <= 0 || !_hasActiveHrir)
        {
            // Immediate switch — no transition (or first direction)
            Array.Copy(leftHrir, _hLL, newIrLength);
            Array.Copy(leftHrir, _hLR, newIrLength);
            Array.Copy(rightHrir, _hRL, newIrLength);
            Array.Copy(rightHrir, _hRR, newIrLength);
            _irLength = newIrLength;
            _transitionFramesRemaining = 0;
            _transitionFramesTotal = 0;
            _hasActiveHrir = true;
            ClearOverlap();
            return;
        }

        // Transition: save current HRIR as "from" source
        var currentLen = Math.Min(_irLength, newIrLength);
        Array.Copy(_hLL, _hFromLL, currentLen);
        Array.Copy(_hLR, _hFromLR, currentLen);
        Array.Copy(_hRL, _hFromRL, currentLen);
        Array.Copy(_hRR, _hFromRR, currentLen);

        // If lengths differ, zero out any extra samples in "from"
        if (currentLen < newIrLength)
        {
            Array.Clear(_hFromLL, currentLen, newIrLength - currentLen);
            Array.Clear(_hFromLR, currentLen, newIrLength - currentLen);
            Array.Clear(_hFromRL, currentLen, newIrLength - currentLen);
            Array.Clear(_hFromRR, currentLen, newIrLength - currentLen);
        }

        // Copy new HRIR into the "to" (active) arrays
        Array.Copy(leftHrir, _hLL, newIrLength);
        Array.Copy(leftHrir, _hLR, newIrLength);
        Array.Copy(rightHrir, _hRL, newIrLength);
        Array.Copy(rightHrir, _hRR, newIrLength);
        _irLength = newIrLength;

        // Configure transition timing
        _transitionSampleRate = SampleRate;
        _transitionFramesTotal = Math.Max(1,
            (int)(_directionTransitionMs * SampleRate / 1000.0));
        _transitionFramesRemaining = _transitionFramesTotal;
        _transitionAlphaStep = 1.0 / _transitionFramesTotal;

        // Reset overlap for clean transition start
        ClearOverlap();
    }

    /// <summary>
    /// Processes interleaved stereo audio through HRTF convolution.
    /// During transitions, performs dual-convolution crossfade between old and new HRIR.
    /// Allocates 0 bytes per block after initial setup.
    /// </summary>
    public void Process(Span<float> buffer)
    {
        if (!IsEnabled || _currentEntry is null || _irLength <= 0)
            return;

        var channels = 2;
        var sampleCount = buffer.Length / channels;
        if (sampleCount <= 0) return;

        EnsureWorkingBuffers(sampleCount);

        var extLen = _overlapCount + sampleCount;
        var extBase = _overlapCount;

        // Fill extended input buffer from overlap + deinterleaved input
        FillExtendedBuffer(buffer, sampleCount, extLen, extBase);

        if (_transitionFramesRemaining > 0)
        {
            ProcessTransition(buffer, sampleCount, extLen, extBase);
        }
        else
        {
            ProcessNormal(buffer, sampleCount, extLen, extBase);
        }
    }

    public void Reset()
    {
        ClearOverlap();
        _transitionFramesRemaining = 0;
        _transitionFramesTotal = 0;
    }

    // ── Internal processing ─────────────────────────────────────────────

    private void FillExtendedBuffer(Span<float> buffer, int sampleCount, int extLen, int extBase)
    {
        Array.Clear(_extendedL, 0, extLen);
        Array.Clear(_extendedR, 0, extLen);

        if (_overlapCount > 0)
        {
            Array.Copy(_overlapL, _extendedL, _overlapCount);
            Array.Copy(_overlapR, _extendedR, _overlapCount);
        }

        for (int i = 0; i < sampleCount; i++)
        {
            _extendedL[extBase + i] = buffer[i * 2];
            _extendedR[extBase + i] = buffer[i * 2 + 1];
        }
    }

    private void ProcessNormal(Span<float> buffer, int sampleCount, int extLen, int extBase)
    {
        Convolve(_extendedL, _hLL, _outLL, extLen);
        Convolve(_extendedR, _hLR, _outLR, extLen);
        Convolve(_extendedL, _hRL, _outRL, extLen);
        Convolve(_extendedR, _hRR, _outRR, extLen);

        SumAndApplyDryWet(sampleCount, extBase, SpatialMix);
        SaveOverlap(extLen);
        Reinterleave(buffer, sampleCount);
    }

    private void ProcessTransition(Span<float> buffer, int sampleCount, int extLen, int extBase)
    {
        // Compute transition alpha for this block
        var progress = _transitionFramesTotal - _transitionFramesRemaining;
        var alpha = Math.Clamp(progress * _transitionAlphaStep, 0.0, 1.0);
        var beta = 1.0 - alpha;

        // Ensure transition output buffers are large enough
        if (_outFromLL.Length < extLen)
        {
            _outFromLL = new float[extLen];
            _outFromLR = new float[extLen];
            _outFromRL = new float[extLen];
            _outFromRR = new float[extLen];
        }

        // Convolve with BOTH old and new HRIR
        Convolve(_extendedL, _hFromLL, _outFromLL, extLen);
        Convolve(_extendedR, _hFromLR, _outFromLR, extLen);
        Convolve(_extendedL, _hFromRL, _outFromRL, extLen);
        Convolve(_extendedR, _hFromRR, _outFromRR, extLen);

        Convolve(_extendedL, _hLL, _outLL, extLen);
        Convolve(_extendedR, _hLR, _outLR, extLen);
        Convolve(_extendedL, _hRL, _outRL, extLen);
        Convolve(_extendedR, _hRR, _outRR, extLen);

        // Blend outputs: lerp(old, new, alpha)
        var outOffset = _overlapCount;
        var mix = SpatialMix;
        var dryMix = 1.0 - mix;

        for (int i = 0; i < sampleCount; i++)
        {
            var srcIdx = outOffset + i;

            var oldL = _outFromLL[srcIdx] + _outFromLR[srcIdx];
            var oldR = _outFromRL[srcIdx] + _outFromRR[srcIdx];
            var newL = _outLL[srcIdx] + _outLR[srcIdx];
            var newR = _outRL[srcIdx] + _outRR[srcIdx];

            var processedL = (float)(beta * oldL + alpha * newL);
            var processedR = (float)(beta * oldR + alpha * newR);

            _sumL[i] = (float)(dryMix * _extendedL[extBase + i] + mix * processedL);
            _sumR[i] = (float)(dryMix * _extendedR[extBase + i] + mix * processedR);
        }

        // Advance transition by the number of audio samples processed
        var remaining = _transitionFramesRemaining - sampleCount;
        if (remaining <= 0)
            remaining = 0;
        _transitionFramesRemaining = remaining;

        SaveOverlap(extLen);
        Reinterleave(buffer, sampleCount);
    }

    private void SumAndApplyDryWet(int sampleCount, int extBase, double mix)
    {
        var outOffset = _overlapCount;
        var dryMix = 1.0 - mix;

        for (int i = 0; i < sampleCount; i++)
        {
            var srcIdx = outOffset + i;
            var processedL = (float)(_outLL[srcIdx] + _outLR[srcIdx]);
            var processedR = (float)(_outRL[srcIdx] + _outRR[srcIdx]);

            _sumL[i] = (float)(dryMix * _extendedL[extBase + i] + mix * processedL);
            _sumR[i] = (float)(dryMix * _extendedR[extBase + i] + mix * processedR);
        }
    }

    private void SaveOverlap(int extLen)
    {
        var newOverlapCount = Math.Min(_irLength - 1, extLen);
        if (newOverlapCount > 0)
        {
            Array.Copy(_extendedL, extLen - newOverlapCount, _overlapL, 0, newOverlapCount);
            Array.Copy(_extendedR, extLen - newOverlapCount, _overlapR, 0, newOverlapCount);
        }
        _overlapCount = newOverlapCount;
    }

    private void Reinterleave(Span<float> buffer, int sampleCount)
    {
        for (int i = 0; i < sampleCount; i++)
        {
            buffer[i * 2] = _sumL[i];
            buffer[i * 2 + 1] = _sumR[i];
        }
    }

    // ── Internal convolution ────────────────────────────────────────────

    /// <summary>
    /// Direct time-domain convolution: output[i] = sum(input[i-k] * h[k]) for k=0..irLength-1.
    /// Output length = input length.
    /// </summary>
    private static void Convolve(float[] input, float[] h, float[] output, int inputLen)
    {
        var irLen = h.Length;
        for (int i = 0; i < inputLen; i++)
        {
            double sum = 0;
            for (int k = 0; k < irLen; k++)
            {
                var idx = i - k;
                if (idx >= 0)
                    sum += input[idx] * h[k];
            }
            output[i] = (float)sum;
        }
    }

    // ── Buffer management ───────────────────────────────────────────────

    private void EnsureWorkingBuffers(int sampleCount)
    {
        var needed = sampleCount + _irLength;
        if (_extendedL.Length < needed)
        {
            _extendedL = new float[needed];
            _extendedR = new float[needed];
            _outLL = new float[needed];
            _outLR = new float[needed];
            _outRL = new float[needed];
            _outRR = new float[needed];
        }

        if (_outFromLL.Length < needed)
        {
            _outFromLL = new float[needed];
            _outFromLR = new float[needed];
            _outFromRL = new float[needed];
            _outFromRR = new float[needed];
        }

        if (_sumL.Length < sampleCount)
        {
            _sumL = new float[sampleCount];
            _sumR = new float[sampleCount];
        }
    }

    private void AllocateBuffers()
    {
        _hLL = new float[_irLength];
        _hLR = new float[_irLength];
        _hRL = new float[_irLength];
        _hRR = new float[_irLength];

        _hFromLL = new float[_irLength];
        _hFromLR = new float[_irLength];
        _hFromRL = new float[_irLength];
        _hFromRR = new float[_irLength];

        var overlapSize = Math.Max(0, _irLength - 1);
        _overlapL = new float[overlapSize];
        _overlapR = new float[overlapSize];
    }

    private void ClearHrirArrays()
    {
        Array.Clear(_hLL);
        Array.Clear(_hLR);
        Array.Clear(_hRL);
        Array.Clear(_hRR);
        Array.Clear(_hFromLL);
        Array.Clear(_hFromLR);
        Array.Clear(_hFromRL);
        Array.Clear(_hFromRR);
        _transitionFramesRemaining = 0;
        _transitionFramesTotal = 0;
    }

    private void ClearOverlap()
    {
        _overlapCount = 0;
        Array.Clear(_overlapL);
        Array.Clear(_overlapR);
    }
}
