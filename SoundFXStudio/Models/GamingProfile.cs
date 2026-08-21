using SoundFXStudio.Infrastructure;
using System.Collections.ObjectModel;
using System.Linq;

namespace SoundFXStudio.Models;

/// <summary>
/// A gaming audio enhancement profile that configures existing DSP effects.
/// Profile != DSP effect: this describes HOW the DSP system should be configured.
/// </summary>
public class GamingProfile : ObservableObject
{
    private string _id = string.Empty;
    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _category = "General";
    private bool _isEnabled;
    private double _preampDb;
    private bool _noiseGateEnabled;
    private double _noiseGateThresholdDb = -45;
    private bool _compressorEnabled;
    private double _compressorThresholdDb = -20;
    private double _compressorRatio = 4;
    private double _compressorAttackMs = 5;
    private double _compressorReleaseMs = 120;
    private double _compressorMakeUpGainDb;
    private bool _limiterEnabled = true;
    private double _limiterThreshold = 0.95;
    private double _limiterReleaseMs = 100;
    private bool _eqEnabled = true;

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public double PreampDb
    {
        get => _preampDb;
        set => SetProperty(ref _preampDb, value);
    }

    public bool NoiseGateEnabled
    {
        get => _noiseGateEnabled;
        set => SetProperty(ref _noiseGateEnabled, value);
    }

    public double NoiseGateThresholdDb
    {
        get => _noiseGateThresholdDb;
        set => SetProperty(ref _noiseGateThresholdDb, value);
    }

    public bool CompressorEnabled
    {
        get => _compressorEnabled;
        set => SetProperty(ref _compressorEnabled, value);
    }

    public double CompressorThresholdDb
    {
        get => _compressorThresholdDb;
        set => SetProperty(ref _compressorThresholdDb, value);
    }

    public double CompressorRatio
    {
        get => _compressorRatio;
        set => SetProperty(ref _compressorRatio, value);
    }

    public double CompressorAttackMs
    {
        get => _compressorAttackMs;
        set => SetProperty(ref _compressorAttackMs, value);
    }

    public double CompressorReleaseMs
    {
        get => _compressorReleaseMs;
        set => SetProperty(ref _compressorReleaseMs, value);
    }

    public double CompressorMakeUpGainDb
    {
        get => _compressorMakeUpGainDb;
        set => SetProperty(ref _compressorMakeUpGainDb, value);
    }

    public bool LimiterEnabled
    {
        get => _limiterEnabled;
        set => SetProperty(ref _limiterEnabled, value);
    }

    public double LimiterThreshold
    {
        get => _limiterThreshold;
        set => SetProperty(ref _limiterThreshold, Math.Clamp(value, 0.1, 1.0));
    }

    public double LimiterReleaseMs
    {
        get => _limiterReleaseMs;
        set => SetProperty(ref _limiterReleaseMs, value);
    }

    public bool EqEnabled
    {
        get => _eqEnabled;
        set => SetProperty(ref _eqEnabled, value);
    }

    public ObservableCollection<EqFilter> EqFilters { get; set; } = new();

    /// <summary>
    /// Future: spatial mode (HRTF, virtual 7.1, etc.)
    /// </summary>
    public string SpatialMode { get; set; } = "Stereo";

    /// <summary>
    /// Future: associated game name.
    /// </summary>
    public string Game { get; set; } = string.Empty;

    /// <summary>
    /// Future: associated headset name.
    /// </summary>
    public string Headset { get; set; } = string.Empty;

    public GamingProfile Clone()
    {
        return new GamingProfile
        {
            Id = Id,
            Name = Name,
            Description = Description,
            Category = Category,
            IsEnabled = IsEnabled,
            PreampDb = PreampDb,
            NoiseGateEnabled = NoiseGateEnabled,
            NoiseGateThresholdDb = NoiseGateThresholdDb,
            CompressorEnabled = CompressorEnabled,
            CompressorThresholdDb = CompressorThresholdDb,
            CompressorRatio = CompressorRatio,
            CompressorAttackMs = CompressorAttackMs,
            CompressorReleaseMs = CompressorReleaseMs,
            CompressorMakeUpGainDb = CompressorMakeUpGainDb,
            LimiterEnabled = LimiterEnabled,
            LimiterThreshold = LimiterThreshold,
            LimiterReleaseMs = LimiterReleaseMs,
            EqEnabled = EqEnabled,
            SpatialMode = SpatialMode,
            Game = Game,
            Headset = Headset,
            EqFilters = new ObservableCollection<EqFilter>(
                EqFilters.Select(f => new EqFilter
                {
                    Type = f.Type,
                    FrequencyHz = f.FrequencyHz,
                    GainDb = f.GainDb,
                    Q = f.Q,
                    Enabled = f.Enabled
                }))
        };
    }

    public override string ToString() => string.IsNullOrWhiteSpace(Name) ? Id : Name;
}
