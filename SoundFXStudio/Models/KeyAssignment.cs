using SoundFXStudio.Infrastructure;

namespace SoundFXStudio.Models;

public class KeyAssignment : ObservableObject
{
    private string _id = Guid.NewGuid().ToString();
    private string _keyId = string.Empty;
    private string _soundId = string.Empty;
    private string? _bindingName;
    private string? _imagePath;
    private string _hotkeyText = string.Empty;
    private bool _isGlobal;
    private float _volumeOverride = 1f;
    private bool _loop;
    private int _fadeOutMs = 120;
    private bool _stopOnReplay = true;

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string KeyId
    {
        get => _keyId;
        set => SetProperty(ref _keyId, value);
    }

    public string SoundId
    {
        get => _soundId;
        set => SetProperty(ref _soundId, value);
    }

    public string? BindingName
    {
        get => _bindingName;
        set => SetProperty(ref _bindingName, value);
    }

    public string? ImagePath
    {
        get => _imagePath;
        set => SetProperty(ref _imagePath, value);
    }

    public string HotkeyText
    {
        get => _hotkeyText;
        set => SetProperty(ref _hotkeyText, value?.Trim().ToUpperInvariant() ?? string.Empty);
    }

    public bool IsGlobal
    {
        get => _isGlobal;
        set => SetProperty(ref _isGlobal, value);
    }

    public float VolumeOverride
    {
        get => _volumeOverride;
        set => SetProperty(ref _volumeOverride, value);
    }

    public bool Loop
    {
        get => _loop;
        set => SetProperty(ref _loop, value);
    }

    public int FadeOutMs
    {
        get => _fadeOutMs;
        set => SetProperty(ref _fadeOutMs, value);
    }

    public bool StopOnReplay
    {
        get => _stopOnReplay;
        set => SetProperty(ref _stopOnReplay, value);
    }
}