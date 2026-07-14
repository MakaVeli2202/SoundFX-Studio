using SoundFXStudio.Infrastructure;
using System.Collections.ObjectModel;

namespace SoundFXStudio.Models;

public class SoundEntry : ObservableObject
{
    private string _id = Guid.NewGuid().ToString();
    private string _name = string.Empty;
    private string _filePath = string.Empty;
    private float _volume = 1f;
    private string? _imagePath;
    private bool _isFavorite;
    private string _hotkey = string.Empty;
    private string _category = "Custom";
    private bool _loop;
    private bool _isMuted;
    private int _playCount;
    private DateTime? _lastPlayedUtc;
    private string? _assignedKeyId;
    private string? _assignedKeyLabel;
    private bool _isMarkedForDelete;

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

    public string FilePath
    {
        get => _filePath;
        set => SetProperty(ref _filePath, value);
    }

    public float Volume
    {
        get => _volume;
        set => SetProperty(ref _volume, value);
    }

    public string? ImagePath
    {
        get => _imagePath;
        set => SetProperty(ref _imagePath, value);
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set => SetProperty(ref _isFavorite, value);
    }

    public string Hotkey
    {
        get => _hotkey;
        set => SetProperty(ref _hotkey, value?.Trim().ToUpperInvariant() ?? string.Empty);
    }

    public string Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }

    public bool Loop
    {
        get => _loop;
        set => SetProperty(ref _loop, value);
    }

    public bool IsMuted
    {
        get => _isMuted;
        set => SetProperty(ref _isMuted, value);
    }

    public int PlayCount
    {
        get => _playCount;
        set => SetProperty(ref _playCount, value);
    }

    public DateTime? LastPlayedUtc
    {
        get => _lastPlayedUtc;
        set => SetProperty(ref _lastPlayedUtc, value);
    }

    public string? AssignedKeyId
    {
        get => _assignedKeyId;
        set => SetProperty(ref _assignedKeyId, value);
    }

    public string? AssignedKeyLabel
    {
        get => _assignedKeyLabel;
        set => SetProperty(ref _assignedKeyLabel, value);
    }

    public bool IsMarkedForDelete
    {
        get => _isMarkedForDelete;
        set => SetProperty(ref _isMarkedForDelete, value);
    }

    public ObservableCollection<string> Tags { get; set; } = new();
}
