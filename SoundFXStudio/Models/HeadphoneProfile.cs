using System.Collections.ObjectModel;
using System.Linq;
using SoundFXStudio.Infrastructure;

namespace SoundFXStudio.Models;

/// <summary>
/// A headphone EQ profile that applies AutoEq parametric EQ compensation.
/// This is independent of GamingProfile — it describes the acoustic correction
/// for a specific headphone model, not game-specific audio processing.
/// </summary>
public class HeadphoneProfile : ObservableObject
{
    private string _id = string.Empty;
    private string _name = string.Empty;
    private string _manufacturer = string.Empty;
    private string _model = string.Empty;
    private string _description = string.Empty;
    private double _preampDb;

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

    public string Manufacturer
    {
        get => _manufacturer;
        set => SetProperty(ref _manufacturer, value);
    }

    public string Model
    {
        get => _model;
        set => SetProperty(ref _model, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public double PreampDb
    {
        get => _preampDb;
        set => SetProperty(ref _preampDb, value);
    }

    public ObservableCollection<EqFilter> Filters { get; set; } = new();

    public HeadphoneProfile Clone()
    {
        return new HeadphoneProfile
        {
            Id = Id,
            Name = Name,
            Manufacturer = Manufacturer,
            Model = Model,
            Description = Description,
            PreampDb = PreampDb,
            Filters = new ObservableCollection<EqFilter>(
                Filters.Select(f => new EqFilter
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
