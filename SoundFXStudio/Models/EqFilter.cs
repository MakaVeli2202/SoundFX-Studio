using SoundFXStudio.Infrastructure;

namespace SoundFXStudio.Models;

/// <summary>
/// A single parametric EQ filter following Equalizer APO / AutoEq conventions.
/// </summary>
public class EqFilter : ObservableObject
{
    private EqFilterType _type = EqFilterType.Peaking;
    private double _frequencyHz = 1000;
    private double _gainDb;
    private double _q = 1.0;
    private bool _enabled = true;

    public EqFilterType Type
    {
        get => _type;
        set => SetProperty(ref _type, value);
    }

    public double FrequencyHz
    {
        get => _frequencyHz;
        set => SetProperty(ref _frequencyHz, Math.Clamp(value, 20, 20000));
    }

    public double GainDb
    {
        get => _gainDb;
        set => SetProperty(ref _gainDb, Math.Clamp(value, -30, 30));
    }

    public double Q
    {
        get => _q;
        set => SetProperty(ref _q, Math.Clamp(value, 0.1, 30));
    }

    public bool Enabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value);
    }
}

public enum EqFilterType
{
    Peaking,
    LowShelf,
    HighShelf,
    LowPass,
    HighPass,
    Notch
}
