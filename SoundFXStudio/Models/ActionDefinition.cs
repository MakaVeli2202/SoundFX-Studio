using SoundFXStudio.Infrastructure;

namespace SoundFXStudio.Models;

public class ActionDefinition : ObservableObject
{
    private Guid _id = Guid.NewGuid();
    private string _name = string.Empty;
    private string _description = string.Empty;
    private ActionType _type;
    private string _iconPath = string.Empty;
    private string _category = string.Empty;
    private string _tags = string.Empty;
    private bool _isFavorite;
    private string _payload = string.Empty;
    private PlaybackMode _playbackMode = PlaybackMode.Restart;
    private KeyPlaybackMode _keyPlaybackMode = KeyPlaybackMode.PlayOnce;
    private string _playlistMode = "Sequential";
    private bool _isEnabled = true;

    public Guid Id
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

    public ActionType Type
    {
        get => _type;
        set => SetProperty(ref _type, value);
    }

    public string IconPath
    {
        get => _iconPath;
        set => SetProperty(ref _iconPath, value);
    }

    public string Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }

    public string Tags
    {
        get => _tags;
        set => SetProperty(ref _tags, value);
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set => SetProperty(ref _isFavorite, value);
    }

    public string Payload
    {
        get => _payload;
        set => SetProperty(ref _payload, value);
    }

    public PlaybackMode PlaybackMode
    {
        get => _playbackMode;
        set => SetProperty(ref _playbackMode, value);
    }

    public KeyPlaybackMode KeyPlaybackMode
    {
        get => _keyPlaybackMode;
        set => SetProperty(ref _keyPlaybackMode, value);
    }

    public string PlaylistMode
    {
        get => _playlistMode;
        set => SetProperty(ref _playlistMode, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }
}
