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

    // Voicemeeter / audio routing (migrated from MyBoard)
    private string _hearDeviceName = string.Empty;
    private string _talkDeviceName = string.Empty;
    private int _speakersDeviceIndex = -1;
    private string _speakersDeviceName = string.Empty;
    private int _virtualCableDeviceIndex = -1;
    private string _virtualCableDeviceName = string.Empty;
    private int _micDeviceIndex = -1;
    private string _micDeviceName = string.Empty;
    private float _pitchShift;
    private float _formantShift = 1f;
    private string _voiceChangerPresetId = "normal";
    private bool _voicePassthroughAutoStart;
    private bool _gamingAudioActive;
    private bool _gamingEnhancementEnabled;
    private string _activeGamingProfileId = string.Empty;
    private bool _headphoneEqEnabled;
    private string _activeHeadphoneProfileId = string.Empty;
    private bool _hrtfEnabled;
    private string _activeHrtfProfileId = string.Empty;
    private double _hrtfAzimuth;
    private double _hrtfElevation;
    private double _hrtfSpatialMix = 1.0;
    private bool _hrtfHeadTrackingEnabled;
    private string _headTrackingProviderId = "opentrack";
    private int _openTrackPort = 4242;
    private string _openTrackBindAddress = "127.0.0.1";
    private AudioLatencyMode _audioLatencyMode = AudioLatencyMode.Balanced;
    private string _savedConsoleMicId = string.Empty;
    private string _savedCommMicId = string.Empty;
    private string _savedDefaultRenderId = string.Empty;
    private string _savedDefaultCaptureId = string.Empty;
    private bool _voicemeeterDetected;

    // Hotkey mode (migrated from MyBoard)
    private string _soundboardToggleKey = "Insert";
    private int _chordTimeoutMs = 1000;
    private int _simultaneousPressTimeoutMs = 200;
    private bool _hotkeyHoldMode = true;

    // Mixer quick-mute hotkeys
    private string _muteAllKey = string.Empty;
    private string _muteHearKey = string.Empty;
    private string _muteTeamKey = string.Empty;
    private string _stopAllKey = string.Empty;
    private string _voiceChangerToggleKey = "C+V";
    private bool _showAllStrips;

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

    // ─── Voicemeeter / audio routing properties ───────────────────────────

    public string HearDeviceName
    {
        get => _hearDeviceName;
        set => SetProperty(ref _hearDeviceName, value);
    }

    public string TalkDeviceName
    {
        get => _talkDeviceName;
        set => SetProperty(ref _talkDeviceName, value);
    }

    public int SpeakersDeviceIndex
    {
        get => _speakersDeviceIndex;
        set => SetProperty(ref _speakersDeviceIndex, value);
    }

    public string SpeakersDeviceName
    {
        get => _speakersDeviceName;
        set => SetProperty(ref _speakersDeviceName, value);
    }

    public int VirtualCableDeviceIndex
    {
        get => _virtualCableDeviceIndex;
        set => SetProperty(ref _virtualCableDeviceIndex, value);
    }

    public string VirtualCableDeviceName
    {
        get => _virtualCableDeviceName;
        set => SetProperty(ref _virtualCableDeviceName, value);
    }

    public int MicDeviceIndex
    {
        get => _micDeviceIndex;
        set => SetProperty(ref _micDeviceIndex, value);
    }

    public string MicDeviceName
    {
        get => _micDeviceName;
        set => SetProperty(ref _micDeviceName, value);
    }

    public float PitchShift
    {
        get => _pitchShift;
        set => SetProperty(ref _pitchShift, value);
    }

    public float FormantShift
    {
        get => _formantShift;
        set => SetProperty(ref _formantShift, value);
    }

    public string VoiceChangerPresetId
    {
        get => _voiceChangerPresetId;
        set => SetProperty(ref _voiceChangerPresetId, string.IsNullOrWhiteSpace(value) ? "normal" : value);
    }

    public bool VoicePassthroughAutoStart
    {
        get => _voicePassthroughAutoStart;
        set => SetProperty(ref _voicePassthroughAutoStart, value);
    }

    public bool GamingAudioActive
    {
        get => _gamingAudioActive;
        set => SetProperty(ref _gamingAudioActive, value);
    }

    public bool GamingEnhancementEnabled
    {
        get => _gamingEnhancementEnabled;
        set => SetProperty(ref _gamingEnhancementEnabled, value);
    }

    public string ActiveGamingProfileId
    {
        get => _activeGamingProfileId;
        set => SetProperty(ref _activeGamingProfileId, value ?? string.Empty);
    }

    public bool HeadphoneEqEnabled
    {
        get => _headphoneEqEnabled;
        set => SetProperty(ref _headphoneEqEnabled, value);
    }

    public string ActiveHeadphoneProfileId
    {
        get => _activeHeadphoneProfileId;
        set => SetProperty(ref _activeHeadphoneProfileId, value ?? string.Empty);
    }

    public bool HrtfEnabled
    {
        get => _hrtfEnabled;
        set => SetProperty(ref _hrtfEnabled, value);
    }

    public string ActiveHrtfProfileId
    {
        get => _activeHrtfProfileId;
        set => SetProperty(ref _activeHrtfProfileId, value ?? string.Empty);
    }

    public double HrtfAzimuth
    {
        get => _hrtfAzimuth;
        set => SetProperty(ref _hrtfAzimuth, value);
    }

    public double HrtfElevation
    {
        get => _hrtfElevation;
        set => SetProperty(ref _hrtfElevation, value);
    }

    public double HrtfSpatialMix
    {
        get => _hrtfSpatialMix;
        set => SetProperty(ref _hrtfSpatialMix, value);
    }

    public bool HrtfHeadTrackingEnabled
    {
        get => _hrtfHeadTrackingEnabled;
        set => SetProperty(ref _hrtfHeadTrackingEnabled, value);
    }

    public string HeadTrackingProviderId
    {
        get => _headTrackingProviderId;
        set => SetProperty(ref _headTrackingProviderId, value ?? "opentrack");
    }

    public int OpenTrackPort
    {
        get => _openTrackPort;
        set => SetProperty(ref _openTrackPort, value);
    }

    public string OpenTrackBindAddress
    {
        get => _openTrackBindAddress;
        set => SetProperty(ref _openTrackBindAddress, value ?? "127.0.0.1");
    }

    public AudioLatencyMode AudioLatencyMode
    {
        get => _audioLatencyMode;
        set => SetProperty(ref _audioLatencyMode, value);
    }

    public string SavedConsoleMicId
    {
        get => _savedConsoleMicId;
        set => SetProperty(ref _savedConsoleMicId, value);
    }

    public string SavedCommMicId
    {
        get => _savedCommMicId;
        set => SetProperty(ref _savedCommMicId, value);
    }

    public string SavedDefaultRenderId
    {
        get => _savedDefaultRenderId;
        set => SetProperty(ref _savedDefaultRenderId, value);
    }

    public string SavedDefaultCaptureId
    {
        get => _savedDefaultCaptureId;
        set => SetProperty(ref _savedDefaultCaptureId, value);
    }

    public bool VoicemeeterDetected
    {
        get => _voicemeeterDetected;
        set => SetProperty(ref _voicemeeterDetected, value);
    }

    // ─── Hotkey mode properties ────────────────────────────────────────────

    public string SoundboardToggleKey
    {
        get => _soundboardToggleKey;
        set => SetProperty(ref _soundboardToggleKey, value);
    }

    public int ChordTimeoutMs
    {
        get => _chordTimeoutMs;
        set => SetProperty(ref _chordTimeoutMs, value);
    }

    public int SimultaneousPressTimeoutMs
    {
        get => _simultaneousPressTimeoutMs;
        set => SetProperty(ref _simultaneousPressTimeoutMs, value);
    }

    public bool HotkeyHoldMode
    {
        get => _hotkeyHoldMode;
        set => SetProperty(ref _hotkeyHoldMode, value);
    }

    // ─── Quick-mute hotkeys (Voicemeeter mixer) ────────────────────────────

    public string MuteAllKey
    {
        get => _muteAllKey;
        set => SetProperty(ref _muteAllKey, value);
    }

    public string MuteHearKey
    {
        get => _muteHearKey;
        set => SetProperty(ref _muteHearKey, value);
    }

    public string MuteTeamKey
    {
        get => _muteTeamKey;
        set => SetProperty(ref _muteTeamKey, value);
    }

    public string StopAllKey
    {
        get => _stopAllKey;
        set => SetProperty(ref _stopAllKey, value);
    }

    public string VoiceChangerToggleKey
    {
        get => _voiceChangerToggleKey;
        set => SetProperty(ref _voiceChangerToggleKey, value);
    }

    public bool ShowAllStrips
    {
        get => _showAllStrips;
        set => SetProperty(ref _showAllStrips, value);
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