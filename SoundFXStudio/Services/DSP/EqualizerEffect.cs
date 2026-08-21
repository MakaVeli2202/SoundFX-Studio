using SoundFXStudio.Models;

namespace SoundFXStudio.Services.DSP;

/// <summary>
/// Parametric equalizer implementing biquad filters for each band.
/// Supports Peaking, LowShelf, HighShelf, LowPass, HighPass, and Notch filter types.
/// Reusable by gaming profiles, headphone profiles, and future features.
/// Filter design follows Equalizer APO / AutoEq conventions (Robert Bristow-Johnson Audio EQ Cookbook).
/// </summary>
public sealed class EqualizerEffect : IAudioEffect
{
    private BiquadState[] _states = Array.Empty<BiquadState>();
    private EqFilter[] _filters = Array.Empty<EqFilter>();
    private int _sampleRate = 48000;
    private bool _dirty = true;
    private BiquadCoefficients[] _coefficients = Array.Empty<BiquadCoefficients>();

    public EqualizerEffect(int sampleRate = 48000)
    {
        _sampleRate = sampleRate;
    }

    public string Name => "Equalizer";

    public bool IsEnabled { get; set; }

    public int SampleRate
    {
        get => _sampleRate;
        set
        {
            if (_sampleRate == value) return;
            _sampleRate = value;
            _dirty = true;
        }
    }

    public double PreampDb { get; set; }

    /// <summary>
    /// Reconfigures the EQ with a new set of filter definitions.
    /// Existing state is discarded and coefficients are recalculated.
    /// </summary>
    public void SetFilters(IReadOnlyList<EqFilter> filters)
    {
        _filters = filters.ToArray();
        _states = new BiquadState[_filters.Length];
        _dirty = true;
    }

    public void Process(Span<float> buffer)
    {
        if (!IsEnabled) return;

        if (_dirty)
        {
            RecalculateCoefficients();
            _dirty = false;
        }

        var preamp = Math.Pow(10.0, PreampDb / 20.0);
        var hasFilters = _filters.Length > 0;

        for (int i = 0; i < buffer.Length; i++)
        {
            double sample = buffer[i] * preamp;

            if (hasFilters)
            {
                for (int f = 0; f < _filters.Length; f++)
                {
                    if (!_filters[f].Enabled) continue;

                    ref var state = ref _states[f];
                    var c = _coefficients[f];

                    var output = c.b0 * sample + c.b1 * state.x1 + c.b2 * state.x2
                                 - c.a1 * state.y1 - c.a2 * state.y2;

                    state.x2 = state.x1;
                    state.x1 = sample;
                    state.y2 = state.y1;
                    state.y1 = output;

                    sample = output;
                }
            }

            buffer[i] = (float)sample;
        }
    }

    public void Reset()
    {
        for (int i = 0; i < _states.Length; i++)
        {
            _states[i] = new BiquadState();
        }
    }

    private void RecalculateCoefficients()
    {
        _coefficients = new BiquadCoefficients[_filters.Length];

        for (int i = 0; i < _filters.Length; i++)
        {
            var f = _filters[i];
            var freq = Math.Clamp(f.FrequencyHz, 20, _sampleRate / 2.0 - 1);
            var q = Math.Max(0.1, f.Q);
            var gain = f.GainDb;

            var w0 = 2.0 * Math.PI * freq / _sampleRate;
            var cosW0 = Math.Cos(w0);
            var sinW0 = Math.Sin(w0);
            var alpha = sinW0 / (2.0 * q);

            // Raw (un-normalized) coefficients from Audio EQ Cookbook
            var raw = f.Type switch
            {
                EqFilterType.Peaking => CalcPeaking(cosW0, alpha, gain),
                EqFilterType.LowShelf => CalcLowShelf(cosW0, sinW0, alpha, gain),
                EqFilterType.HighShelf => CalcHighShelf(cosW0, sinW0, alpha, gain),
                EqFilterType.LowPass => CalcLowPass(cosW0, alpha),
                EqFilterType.HighPass => CalcHighPass(cosW0, alpha),
                EqFilterType.Notch => CalcNotch(cosW0, alpha),
                _ => CalcPeaking(cosW0, alpha, gain)
            };

            // Normalize by a0 so that a0 becomes 1.0
            var invA0 = 1.0 / raw.a0;
            _coefficients[i] = new BiquadCoefficients(
                b0: raw.b0 * invA0,
                b1: raw.b1 * invA0,
                b2: raw.b2 * invA0,
                a1: raw.a1 * invA0,
                a2: raw.a2 * invA0
            );
        }
    }

    // Audio EQ Cookbook formulas (Robert Bristow-Johnson)

    private static RawCoefficients CalcPeaking(double cosW0, double alpha, double gainDb)
    {
        var A = Math.Pow(10.0, gainDb / 40.0);
        return new RawCoefficients(
            b0: 1.0 + alpha * A,
            b1: -2.0 * cosW0,
            b2: 1.0 - alpha * A,
            a0: 1.0 + alpha / A,
            a1: -2.0 * cosW0,
            a2: 1.0 - alpha / A
        );
    }

    private static RawCoefficients CalcLowShelf(double cosW0, double sinW0, double alpha, double gainDb)
    {
        var A = Math.Pow(10.0, gainDb / 40.0);
        var twoSqrtA = 2.0 * Math.Sqrt(A);
        return new RawCoefficients(
            b0: A * ((A + 1.0) - (A - 1.0) * cosW0 + twoSqrtA * alpha),
            b1: 2.0 * A * ((A - 1.0) - (A + 1.0) * cosW0),
            b2: A * ((A + 1.0) - (A - 1.0) * cosW0 - twoSqrtA * alpha),
            a0: (A + 1.0) + (A - 1.0) * cosW0 + twoSqrtA * alpha,
            a1: -2.0 * ((A - 1.0) + (A + 1.0) * cosW0),
            a2: (A + 1.0) + (A - 1.0) * cosW0 - twoSqrtA * alpha
        );
    }

    private static RawCoefficients CalcHighShelf(double cosW0, double sinW0, double alpha, double gainDb)
    {
        var A = Math.Pow(10.0, gainDb / 40.0);
        var twoSqrtA = 2.0 * Math.Sqrt(A);
        return new RawCoefficients(
            b0: A * ((A + 1.0) + (A - 1.0) * cosW0 + twoSqrtA * alpha),
            b1: -2.0 * A * ((A - 1.0) + (A + 1.0) * cosW0),
            b2: A * ((A + 1.0) + (A - 1.0) * cosW0 - twoSqrtA * alpha),
            a0: (A + 1.0) - (A - 1.0) * cosW0 + twoSqrtA * alpha,
            a1: 2.0 * ((A - 1.0) - (A + 1.0) * cosW0),
            a2: (A + 1.0) - (A - 1.0) * cosW0 - twoSqrtA * alpha
        );
    }

    private static RawCoefficients CalcLowPass(double cosW0, double alpha)
    {
        return new RawCoefficients(
            b0: (1.0 - cosW0) / 2.0,
            b1: 1.0 - cosW0,
            b2: (1.0 - cosW0) / 2.0,
            a0: 1.0 + alpha,
            a1: -2.0 * cosW0,
            a2: 1.0 - alpha
        );
    }

    private static RawCoefficients CalcHighPass(double cosW0, double alpha)
    {
        return new RawCoefficients(
            b0: (1.0 + cosW0) / 2.0,
            b1: -(1.0 + cosW0),
            b2: (1.0 + cosW0) / 2.0,
            a0: 1.0 + alpha,
            a1: -2.0 * cosW0,
            a2: 1.0 - alpha
        );
    }

    private static RawCoefficients CalcNotch(double cosW0, double alpha)
    {
        return new RawCoefficients(
            b0: 1.0,
            b1: -2.0 * cosW0,
            b2: 1.0,
            a0: 1.0 + alpha,
            a1: -2.0 * cosW0,
            a2: 1.0 - alpha
        );
    }

    private readonly record struct RawCoefficients(
        double b0, double b1, double b2,
        double a0, double a1, double a2);

    private readonly record struct BiquadCoefficients(
        double b0, double b1, double b2,
        double a1, double a2);

    private struct BiquadState
    {
        public double x1, x2, y1, y2;
    }
}
