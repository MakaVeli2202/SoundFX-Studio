using SoundFXStudio.Infrastructure;
using System.Collections.ObjectModel;

namespace SoundFXStudio.Models;

public class AppConfig : ObservableObject
{
    private string _activeProfileId = string.Empty;

    public ObservableCollection<SoundEntry> Sounds { get; set; } = new();

    public ObservableCollection<ActionDefinition> Actions { get; set; } = new();

    public ObservableCollection<ComboDefinition> Combos { get; set; } = new();

    public ObservableCollection<KeyChord> KeyChords { get; set; } = new();

    public ObservableCollection<PlaylistDefinition> Playlists { get; set; } = new();

    public ObservableCollection<MacroDefinition> Macros { get; set; } = new();

    public ObservableCollection<AudioRoutingPreset> RoutingPresets { get; set; } = new();

    public ObservableCollection<Profile> Profiles { get; set; } = new();

    public ObservableCollection<Category> Categories { get; set; } = new();

    public AppSettings Settings { get; set; } = new();

    public string ActiveProfileId
    {
        get => _activeProfileId;
        set => SetProperty(ref _activeProfileId, value);
    }
}
