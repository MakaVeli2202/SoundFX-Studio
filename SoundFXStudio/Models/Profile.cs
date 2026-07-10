using SoundFXStudio.Infrastructure;
using System.Collections.ObjectModel;

namespace SoundFXStudio.Models;

public class Profile : ObservableObject
{
    private string _id = Guid.NewGuid().ToString();
    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _accentColor = "#00D4FF";
    private bool _isDefault;

    public ObservableCollection<ActionDefinition> Actions { get; set; } = new();

    public ObservableCollection<ComboDefinition> Combos { get; set; } = new();

    public ObservableCollection<KeyChord> KeyChords { get; set; } = new();

    public ObservableCollection<PlaylistDefinition> Playlists { get; set; } = new();

    public ObservableCollection<MacroDefinition> Macros { get; set; } = new();

    public ObservableCollection<AudioRoutingPreset> RoutingPresets { get; set; } = new();

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

    public string AccentColor
    {
        get => _accentColor;
        set => SetProperty(ref _accentColor, value);
    }

    public bool IsDefault
    {
        get => _isDefault;
        set => SetProperty(ref _isDefault, value);
    }

    public ObservableCollection<KeyAssignment> Assignments { get; set; } = new();
}
