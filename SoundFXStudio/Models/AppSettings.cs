using SoundFXStudio.Infrastructure;

namespace SoundFXStudio.Models;

public class AppSettings : ObservableObject
{
    private string _inputDeviceId = string.Empty;
    private string _outputDeviceId = string.Empty;
    private string _playbackDeviceId = string.Empty;
    private string _microphoneDeviceId = string.Empty;
    private string _virtualCableDeviceId = string.Empty;
    private KeyboardLayoutMode _keyboardLayout = KeyboardLayoutMode.Automatic;
    private float _masterVolume = 1f;
    private bool _enableGlobalHotkeys = true;
    private bool _enableLogging = true;
    private bool _advancedMode;
    private bool _showSetupWizardOnStartup = true;
    private bool _startMinimized;
    private bool _allowMultipleInstances;
    private DateTime? _lastConfigurationDate;
    private bool _vbcableDetected;
    private bool _defaultSoundsSeeded;
    private bool _setupCompleted;
    private string _keyboardPressedTextColor = "#22D3FF";
    private KeyboardCalibrationSettings _keyboardCalibration = new();

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

    public bool EnableLogging
    {
        get => _enableLogging;
        set => SetProperty(ref _enableLogging, value);
    }

    public bool AdvancedMode
    {
        get => _advancedMode;
        set => SetProperty(ref _advancedMode, value);
    }

    public bool ShowSetupWizardOnStartup
    {
        get => _showSetupWizardOnStartup;
        set => SetProperty(ref _showSetupWizardOnStartup, value);
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

    public string KeyboardPressedTextColor
    {
        get => _keyboardPressedTextColor;
        set => SetProperty(ref _keyboardPressedTextColor, string.IsNullOrWhiteSpace(value) ? "#22D3FF" : value.Trim());
    }

    public KeyboardCalibrationSettings KeyboardCalibration
    {
        get => _keyboardCalibration;
        set => SetProperty(ref _keyboardCalibration, value ?? new KeyboardCalibrationSettings());
    }
}