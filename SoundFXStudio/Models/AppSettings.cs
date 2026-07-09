using SoundFXStudio.Infrastructure;

namespace SoundFXStudio.Models;

public class AppSettings : ObservableObject
{
    private string _inputDeviceId = string.Empty;
    private string _outputDeviceId = string.Empty;
    private string _playbackDeviceId = string.Empty;
    private string _microphoneDeviceId = string.Empty;
    private string _virtualCableDeviceId = string.Empty;
    private string _theme = "Dark Neon";
    private KeyboardLayoutMode _keyboardLayout = KeyboardLayoutMode.English;
    private float _masterVolume = 1f;
    private bool _enableGlobalHotkeys = true;
    private bool _startMinimized;
    private bool _allowMultipleInstances;
    private DateTime? _lastConfigurationDate;
    private bool _vbcableDetected;
    private bool _defaultSoundsSeeded;
    private bool _setupCompleted;

    public string InputDeviceId
    {
        get => _inputDeviceId;
        set => SetProperty(ref _inputDeviceId, value);
    }

    public string OutputDeviceId
    {
        get => _outputDeviceId;
        set => SetProperty(ref _outputDeviceId, value);
    }

    public string PlaybackDeviceId
    {
        get => _playbackDeviceId;
        set => SetProperty(ref _playbackDeviceId, value);
    }

    public string MicrophoneDeviceId
    {
        get => _microphoneDeviceId;
        set => SetProperty(ref _microphoneDeviceId, value);
    }

    public string VirtualCableDeviceId
    {
        get => _virtualCableDeviceId;
        set => SetProperty(ref _virtualCableDeviceId, value);
    }

    public string Theme
    {
        get => _theme;
        set => SetProperty(ref _theme, value);
    }

    public KeyboardLayoutMode KeyboardLayout
    {
        get => _keyboardLayout;
        set => SetProperty(ref _keyboardLayout, value);
    }

    public float MasterVolume
    {
        get => _masterVolume;
        set => SetProperty(ref _masterVolume, value);
    }

    public bool EnableGlobalHotkeys
    {
        get => _enableGlobalHotkeys;
        set => SetProperty(ref _enableGlobalHotkeys, value);
    }

    public bool StartMinimized
    {
        get => _startMinimized;
        set => SetProperty(ref _startMinimized, value);
    }

    public bool AllowMultipleInstances
    {
        get => _allowMultipleInstances;
        set => SetProperty(ref _allowMultipleInstances, value);
    }

    public DateTime? LastConfigurationDate
    {
        get => _lastConfigurationDate;
        set => SetProperty(ref _lastConfigurationDate, value);
    }

    public bool VBCableDetected
    {
        get => _vbcableDetected;
        set => SetProperty(ref _vbcableDetected, value);
    }

    public bool DefaultSoundsSeeded
    {
        get => _defaultSoundsSeeded;
        set => SetProperty(ref _defaultSoundsSeeded, value);
    }

    public bool SetupCompleted
    {
        get => _setupCompleted;
        set => SetProperty(ref _setupCompleted, value);
    }
}