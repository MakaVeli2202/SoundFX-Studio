using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using Microsoft.Win32;
using SoundFXStudio.Infrastructure;
using SoundFXStudio.Models;
using SoundFXStudio.Services;
using SoundFXStudio.Services.Diagnostics;
using SoundFXStudio.Services.DSP;
using SoundFXStudio.Services.Hrtf;
using SoundFXStudio.Services.ArtTune;

namespace SoundFXStudio.ViewModels;

public sealed class GamingViewModel : ObservableObject, IDisposable
{
    private readonly GameAudioService _gameAudioService = new();
    private readonly Dictionary<string, uint> _processPidMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action? _saveAction;
    private readonly Action<string>? _setStatusAction;
    private readonly AppSettings? _settings;
    private readonly ISofaHrtfLoader _sofaLoader;
    private readonly IHrtfProfileStore _profileStore;
    private readonly ArtTuneLibraryService _artTuneLibrary = new();
    private readonly ArtTuneStackService _artTuneStack = new();
    private ArtTuneStackState _artTuneStackState = ArtTuneStackService.Detect();
    private ArtTuneTuningVerification _artTuneTuning = ArtTuneTuningVerification.Empty;
    private System.Windows.Threading.DispatcherTimer? _artTuneHealthTimer;
    private string _tuningHealthText = "TUNING NOT VERIFIED";
    private System.Windows.Media.Brush _tuningHealthColor = System.Windows.Media.Brushes.DarkGray;
    private string _tuningHealthDetail = string.Empty;
    private bool _isArtTuneBusy;
    private string _artTuneRunLog = string.Empty;
    private string _selectedArtTuneVersion = string.Empty;
    private string _selectedArtTuneSixteenCh = string.Empty;
    private List<ArtTuneStackService.InstalledTuneVersion> _installedTuneVersions = new();
    private bool _disposed;
    private bool _isEnabled;
    private bool _isCapturing;
    private bool _isTuneLibraryUpdating;
    private string _tuneLibraryStatus = "Not loaded";
    private string _statusText = "Ready";
    private string _errorText = string.Empty;
    private string _headTrackingProviderName = "None";
    private string _headTrackingProviderStatus = "Unavailable";
    private GameProcessInfo? _selectedProcess;
    private GamingProfile? _selectedProfile;
    private int _selectedProfileIndex;
    private HeadphoneProfile? _selectedHeadphoneProfile;
    private bool _isHeadphoneEqEnabled;
    private HrtfProfile? _selectedHrtfProfile;
    private bool _isHrtfEnabled;
    private double _hrtfAzimuth;
    private double _hrtfElevation;
    private double _hrtfSpatialMixPct = 100.0;
    private bool _isHrtfHeadTrackingEnabled;
    private double _headYaw;
    private double _headPitch;
    private double _headRoll;
    private readonly Services.Hrtf.HeadTrackingService _headTrackingService = new();
    private readonly Services.Hrtf.HeadTrackingProviderFactory _providerFactory = new();
    private System.Windows.Threading.DispatcherTimer? _headTrackingTimer;
    private Services.Hrtf.IHeadTrackingProvider? _currentProvider;

    // Latency diagnostics
    private AudioLatencyMode _selectedLatencyMode = AudioLatencyMode.Balanced;
    private System.Windows.Threading.DispatcherTimer? _diagnosticsTimer;
    private double _dspP95Us;
    private double _dspP99Us;
    private double _dspBudgetPercent;
    private long _overBudgetBlockCount;
    private string _latencyHealthStatus = "Healthy";
    private double _estimatedPipelineLatencyMs;
    private double _configuredOutputLatencyMs;
    private double _captureToDspLatencyMs;
    private long _captureStarvationCount;
    private string _latencyModeSafetyMessage = string.Empty;
    private readonly Services.Diagnostics.AudioProductionHealthMonitor _healthMonitor = new();

    private static readonly HashSet<string> ExcludedProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "svchost", "csrss", "dwm", "msedge", "chrome", "firefox", "code",
        "devenv", "SearchApp", "ShellExperienceHost", "SystemSettings",
        "conhost", "cmd", "powershell", "explorer", "taskhostw",
        "StartMenuExperienceHost", "RuntimeBroker", "ApplicationFrameHost",
        "ctfmon", "sihost", "fontdrvhost", "dasHost", "audiodg"
    };

    private static readonly HashSet<string> PresetProfileIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "none", "synthetic-front", "synthetic-above", "synthetic-left"
    };

    // Game window-title keywords mapped to ArtTuneDB tune categories.
    private static readonly (string Game, string[] Keywords)[] GameKeywords =
    {
        ("BO6", new[] { "black ops", "bo6", "call of duty", "cod" }),
        ("BF6", new[] { "battlefield", "bf6" }),
        ("MW", new[] { "warzone", "wz", "modern warfare", "mw3", "mw2" }),
        ("CS2", new[] { "counter-strike", "cs2", "csgo" }),
        ("VAL", new[] { "valorant" }),
        ("APEX", new[] { "apex legends", "apex" }),
        ("FN", new[] { "fortnite" }),
        ("PUBG", new[] { "pubg" }),
        ("HUNT", new[] { "hunt showdown", "hunt" }),
        ("EFT", new[] { "tarkov", "eft" })
    };

    public GamingViewModel(
        Action? saveAction = null,
        Action<string>? setStatusAction = null,
        AppSettings? settings = null,
        ISofaHrtfLoader? sofaLoader = null,
        IHrtfProfileStore? profileStore = null)
    {
        _saveAction = saveAction;
        _setStatusAction = setStatusAction;
        _settings = settings;
        _sofaLoader = sofaLoader ?? new SofaHrtfLoader();
        _profileStore = profileStore ?? new HrtfProfileStore();

        StartCaptureCommand = new RelayCommand(_ => StartCapture(), _ => CanStartCapture);
        StopCaptureCommand = new RelayCommand(_ => StopCapture(), _ => IsCapturing);
        ResetToDefaultsCommand = new RelayCommand(_ => ResetSystemToDefaults());
        RefreshProcessesCommand = new RelayCommand(_ => RefreshProcesses());
        ToggleEnableCommand = new RelayCommand(_ => IsEnabled = !IsEnabled);
        ToggleHeadphoneEqCommand = new RelayCommand(_ => IsHeadphoneEqEnabled = !IsHeadphoneEqEnabled);
        ToggleHrtfCommand = new RelayCommand(_ => IsHrtfEnabled = !IsHrtfEnabled);
        ImportHrtfProfileCommand = new AsyncRelayCommand(_ => ImportHrtfProfileAsync());
        DeleteHrtfProfileCommand = new RelayCommand(_ => DeleteHrtfProfile(), _ => CanDeleteHrtfProfile);
        CalibrateHeadTrackingCommand = new RelayCommand(_ => CalibrateHeadTracking(), _ => IsHrtfHeadTrackingEnabled);
        UpdateTuneLibraryCommand = new AsyncRelayCommand(async _ => await UpdateTuneLibraryAsync());
        InstallArtTuneStackCommand = new AsyncRelayCommand(async _ => await InstallArtTuneStackAsync(), _ => !IsArtTuneBusy);
        ApplyArtTuneTuneCommand = new AsyncRelayCommand(async _ => await ApplyArtTuneTuneAsync(), _ => !IsArtTuneBusy);
        RefreshArtTuneStackCommand = new RelayCommand(_ => RefreshArtTuneStack());
        OpenArtTuneGuideCommand = new RelayCommand(_ => ArtTuneStackService.OpenGuidedGuide());
        RollbackArtTuneStackCommand = new AsyncRelayCommand(async _ => await RollbackArtTuneStackAsync(), _ => !IsArtTuneBusy);

        foreach (var profile in GamingProfilePresets.Profiles)
            AvailableProfiles.Add(profile);
        SelectedProfile = AvailableProfiles.FirstOrDefault();

        AvailableHeadphoneProfiles.Add(HeadphoneProfilePresets.GetNone());
        foreach (var hp in HeadphoneProfilePresets.Profiles)
            AvailableHeadphoneProfiles.Add(hp);
        SelectedHeadphoneProfile = AvailableHeadphoneProfiles.FirstOrDefault();

        AvailableHrtfProfiles.Add(HrtfProfilePresets.GetNone());
        foreach (var hrtf in HrtfProfilePresets.Profiles)
            AvailableHrtfProfiles.Add(hrtf);

        // Load persisted imported profiles
        try
        {
            foreach (var imported in _profileStore.LoadAll())
                AvailableHrtfProfiles.Add(imported);
        }
        catch { /* corrupt/missing store — use defaults */ }

        LoadCachedArtTuneLibrary();

        SelectedHrtfProfile = AvailableHrtfProfiles.FirstOrDefault();

        if (_settings is not null)
            RestoreSettings();

        try { InitializeHeadTrackingProvider(); } catch { /* provider init failed — head tracking unavailable */ }

        AvailableLatencyModes.Add(AudioLatencyMode.Safe);
        AvailableLatencyModes.Add(AudioLatencyMode.Balanced);
        AvailableLatencyModes.Add(AudioLatencyMode.LowLatency);

        RefreshProcesses();

        StartArtTuneHealthTimer();

        _gameAudioService.CaptureStarted += OnCaptureStarted;
        _gameAudioService.CaptureStopped += OnCaptureStopped;
        _gameAudioService.CaptureError += OnCaptureError;
    }

    public GameAudioService GameAudioService => _gameAudioService;

    public ObservableCollection<GamingProfile> AvailableProfiles { get; } = new();
    public ObservableCollection<HeadphoneProfile> AvailableHeadphoneProfiles { get; } = new();
    public ObservableCollection<HrtfProfile> AvailableHrtfProfiles { get; } = new();
    public ObservableCollection<GameProcessInfo> RunningProcesses { get; } = new();
    public ObservableCollection<AudioLatencyMode> AvailableLatencyModes { get; } = new();

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value))
            {
                OnPropertyChanged(nameof(EnableToggleText));
                OnPropertyChanged(nameof(EnableStatusColor));
                StatusText = value ? "Gaming Enhancement enabled" : "Gaming Enhancement disabled";
                OnPropertyChanged(nameof(CanStartCapture));
                StartCaptureCommand.RaiseCanExecuteChanged();
                _saveAction?.Invoke();
            }
        }
    }

    public string EnableToggleText => IsEnabled ? "ENABLED" : "DISABLED";
    public string EnableStatusColor => IsEnabled ? "#22C55E" : "#E85555";

    public bool IsCapturing
    {
        get => _isCapturing;
        private set
        {
            if (SetProperty(ref _isCapturing, value))
            {
                OnPropertyChanged(nameof(CaptureButtonText));
                OnPropertyChanged(nameof(CanStartCapture));
                StartCaptureCommand.RaiseCanExecuteChanged();
                StopCaptureCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string CaptureButtonText => IsCapturing ? "Stop Capture" : "Start Capture";

    public bool CanStartCapture => !IsCapturing && SelectedProcess is not null && IsEnabled;

    public string StatusText
    {
        get => _statusText;
        set
        {
            if (SetProperty(ref _statusText, value))
            {
                _setStatusAction?.Invoke(value);
                if (!string.IsNullOrWhiteSpace(value))
                    Services.ActionLog.Instance.Info("Gaming", value);
            }
        }
    }

    public string ErrorText
    {
        get => _errorText;
        set
        {
            if (SetProperty(ref _errorText, value) && !string.IsNullOrWhiteSpace(value))
                Services.ActionLog.Instance.Error("Gaming", $"ErrorText: {value}");
        }
    }

    public GameProcessInfo? SelectedProcess
    {
        get => _selectedProcess;
        set
        {
            if (SetProperty(ref _selectedProcess, value))
            {
                OnPropertyChanged(nameof(CanStartCapture));
                StartCaptureCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public GamingProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetProperty(ref _selectedProfile, value) && value is not null)
            {
                _gameAudioService.Enhancement.Apply(value);
                StatusText = $"Profile: {value.Name}";
                _saveAction?.Invoke();
            }
        }
    }

    public int SelectedProfileIndex
    {
        get => _selectedProfileIndex;
        set
        {
            if (SetProperty(ref _selectedProfileIndex, value) && value >= 0 && value < AvailableProfiles.Count)
            {
                SelectedProfile = AvailableProfiles[value];
            }
        }
    }

    public RelayCommand StartCaptureCommand { get; }
    public RelayCommand StopCaptureCommand { get; }
    public RelayCommand RefreshProcessesCommand { get; }
    public RelayCommand ResetToDefaultsCommand { get; }
    public RelayCommand ToggleEnableCommand { get; }
    public RelayCommand ToggleHeadphoneEqCommand { get; }
    public RelayCommand ToggleHrtfCommand { get; }
    public AsyncRelayCommand ImportHrtfProfileCommand { get; }
    public RelayCommand DeleteHrtfProfileCommand { get; }
    public RelayCommand CalibrateHeadTrackingCommand { get; }
    public AsyncRelayCommand UpdateTuneLibraryCommand { get; }

    public bool IsTuneLibraryUpdating
    {
        get => _isTuneLibraryUpdating;
        private set => SetProperty(ref _isTuneLibraryUpdating, value);
    }

    public string TuneLibraryStatus
    {
        get => _tuneLibraryStatus;
        private set => SetProperty(ref _tuneLibraryStatus, value);
    }

    public bool IsHeadphoneEqEnabled
    {
        get => _isHeadphoneEqEnabled;
        set
        {
            if (SetProperty(ref _isHeadphoneEqEnabled, value))
            {
                OnPropertyChanged(nameof(HeadphoneEqToggleText));
                OnPropertyChanged(nameof(HeadphoneEqStatusColor));
                ApplyHeadphoneProfile();
                _saveAction?.Invoke();
            }
        }
    }

    public string HeadphoneEqToggleText => IsHeadphoneEqEnabled ? "ENABLED" : "DISABLED";
    public string HeadphoneEqStatusColor => IsHeadphoneEqEnabled ? "#22C55E" : "#E85555";

    public HeadphoneProfile? SelectedHeadphoneProfile
    {
        get => _selectedHeadphoneProfile;
        set
        {
            if (SetProperty(ref _selectedHeadphoneProfile, value))
            {
                ApplyHeadphoneProfile();
                _saveAction?.Invoke();
            }
        }
    }

    private void ApplyHeadphoneProfile()
    {
        if (IsHeadphoneEqEnabled && SelectedHeadphoneProfile is not null
            && !string.Equals(SelectedHeadphoneProfile.Id, "none", StringComparison.OrdinalIgnoreCase))
        {
            _gameAudioService.Enhancement.ApplyHeadphoneProfile(SelectedHeadphoneProfile);
        }
        else
        {
            _gameAudioService.Enhancement.ApplyHeadphoneProfile(null);
        }
    }

    public bool IsHrtfEnabled
    {
        get => _isHrtfEnabled;
        set
        {
            if (SetProperty(ref _isHrtfEnabled, value))
            {
                OnPropertyChanged(nameof(HrtfToggleText));
                OnPropertyChanged(nameof(HrtfStatusColor));
                OnPropertyChanged(nameof(HeadTrackingStatus));
                ApplyHrtfProfile();
                if (value)
                {
                    UpdateHrtfDirection();
                    _gameAudioService.Enhancement.HrtfSpatializer.SpatialMix = _hrtfSpatialMixPct / 100.0;
                }
                _saveAction?.Invoke();
            }
        }
    }

    public string HrtfToggleText => IsHrtfEnabled ? "ENABLED" : "DISABLED";
    public string HrtfStatusColor => IsHrtfEnabled ? "#22C55E" : "#E85555";

    public HrtfProfile? SelectedHrtfProfile
    {
        get => _selectedHrtfProfile;
        set
        {
            if (SetProperty(ref _selectedHrtfProfile, value))
            {
                ApplyHrtfProfile();
                OnPropertyChanged(nameof(CanDeleteHrtfProfile));
                DeleteHrtfProfileCommand.RaiseCanExecuteChanged();
                _saveAction?.Invoke();
            }
        }
    }

    private void ApplyHrtfProfile()
    {
        if (IsHrtfEnabled && SelectedHrtfProfile is not null
            && !string.Equals(SelectedHrtfProfile.Id, "none", StringComparison.OrdinalIgnoreCase))
        {
            _gameAudioService.Enhancement.ApplyHrtfProfile(SelectedHrtfProfile);
            UpdateHrtfDirection();
            _gameAudioService.Enhancement.HrtfSpatializer.SpatialMix = _hrtfSpatialMixPct / 100.0;
        }
        else
        {
            _gameAudioService.Enhancement.ApplyHrtfProfile(null);
        }
    }

    public double HrtfAzimuth
    {
        get => _hrtfAzimuth;
        set
        {
            value = Math.Clamp(value, -180.0, 180.0);
            if (SetProperty(ref _hrtfAzimuth, value))
            {
                UpdateHrtfDirection();
                PersistHrtfDirection();
            }
        }
    }

    public double HrtfElevation
    {
        get => _hrtfElevation;
        set
        {
            value = Math.Clamp(value, -90.0, 90.0);
            if (SetProperty(ref _hrtfElevation, value))
            {
                UpdateHrtfDirection();
                PersistHrtfDirection();
            }
        }
    }

    /// <summary>
    /// Spatial intensity as a percentage (0–100). Mapped to HrtfEffect.SpatialMix (0.0–1.0).
    /// </summary>
    public double HrtfSpatialMixPct
    {
        get => _hrtfSpatialMixPct;
        set
        {
            value = Math.Clamp(value, 0.0, 100.0);
            if (SetProperty(ref _hrtfSpatialMixPct, value))
            {
                _gameAudioService.Enhancement.HrtfSpatializer.SpatialMix = value / 100.0;
                PersistHrtfSpatialMix();
            }
        }
    }

    // ─── Head Tracking properties ───────────────────────────────────────

    public bool IsHrtfHeadTrackingEnabled
    {
        get => _isHrtfHeadTrackingEnabled;
        set
        {
            if (SetProperty(ref _isHrtfHeadTrackingEnabled, value))
            {
                OnPropertyChanged(nameof(HeadTrackingToggleText));
                OnPropertyChanged(nameof(HeadTrackingStatusColor));
                OnPropertyChanged(nameof(HeadTrackingStatus));
                OnPropertyChanged(nameof(CanCalibrateHeadTracking));

                if (value)
                {
                    StartHeadTracking();
                }
                else
                {
                    StopHeadTracking();
                }

                _saveAction?.Invoke();
            }
        }
    }

    public string HeadTrackingToggleText => IsHrtfHeadTrackingEnabled ? "ENABLED" : "DISABLED";
    public string HeadTrackingStatusColor => IsHrtfHeadTrackingEnabled ? "#22C55E" : "#E85555";

    public string HeadTrackingStatus
    {
        get
        {
            if (!_headTrackingService.IsAvailable) return "Unavailable";
            if (!IsHrtfHeadTrackingEnabled) return "Ready";
            return _headTrackingService.IsTracking ? "Tracking" : "Ready";
        }
    }

    public string HeadTrackingProviderName
    {
        get => _headTrackingProviderName;
        private set => SetProperty(ref _headTrackingProviderName, value);
    }

    public string HeadTrackingProviderStatus
    {
        get => _headTrackingProviderStatus;
        private set => SetProperty(ref _headTrackingProviderStatus, value);
    }

    public string HeadTrackingProviderStatusColor
    {
        get
        {
            return _headTrackingProviderStatus switch
            {
                "Tracking" => "#22C55E",
                "Ready" => "#FBBF24",
                "Error" => "#E85555",
                _ => "#6B7280"
            };
        }
    }

    public bool HeadTrackingAvailable => _headTrackingService.IsAvailable;
    public bool CanCalibrateHeadTracking => IsHrtfHeadTrackingEnabled;

    public double HeadYaw
    {
        get => _headYaw;
        private set => SetProperty(ref _headYaw, value);
    }

    public double HeadPitch
    {
        get => _headPitch;
        private set => SetProperty(ref _headPitch, value);
    }

    public double HeadRoll
    {
        get => _headRoll;
        private set => SetProperty(ref _headRoll, value);
    }

    /// <summary>
    /// Called by a timer or polling mechanism to update head tracking direction.
    /// Rate limiting and angle threshold filtering happen inside HeadTrackingService.
    /// </summary>
    public void UpdateHeadTracking()
    {
        if (!IsHrtfHeadTrackingEnabled || !IsHrtfEnabled) return;

        var result = _headTrackingService.Update(
            _gameAudioService.Enhancement.HrtfSpatializer,
            headTrackingEnabled: true,
            hrtfEnabled: true);

        if (result is { } dir)
        {
            _hrtfAzimuth = dir.AzimuthDeg;
            _hrtfElevation = dir.ElevationDeg;
            OnPropertyChanged(nameof(HrtfAzimuth));
            OnPropertyChanged(nameof(HrtfElevation));

            var orientation = _headTrackingService.Provider.GetOrientation();
            HeadYaw = orientation.YawDeg;
            HeadPitch = orientation.PitchDeg;
            HeadRoll = orientation.RollDeg;
        }
    }

    private void CalibrateHeadTracking()
    {
        _headTrackingService.Calibrate();
        StatusText = "Head tracking calibrated";
    }

    private void InitializeHeadTrackingProvider()
    {
        if (_settings is null) return;

        var providerId = _settings.HeadTrackingProviderId;
        var options = new Services.Hrtf.OpenTrackHeadTrackingOptions
        {
            Port = _settings.OpenTrackPort,
            BindAddress = _settings.OpenTrackBindAddress
        };

        _currentProvider?.Dispose();
        _currentProvider = _providerFactory.Create(providerId, options);
        _headTrackingService.Provider = _currentProvider;

        HeadTrackingProviderName = _currentProvider.ProviderName;
        UpdateProviderStatus();
    }

    private void StartHeadTracking()
    {
        if (_currentProvider is null) InitializeHeadTrackingProvider();
        if (_currentProvider is null) return;

        var started = _headTrackingService.Start();
        UpdateProviderStatus();

        if (started)
        {
            _headTrackingTimer ??= new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 Hz
            };
            _headTrackingTimer.Tick += HeadTrackingTimer_Tick;
            _headTrackingTimer.Start();
            StatusText = $"Head tracking started ({HeadTrackingProviderName})";
        }
        else
        {
            var error = (_currentProvider as Services.Hrtf.OpenTrackHeadTrackingProvider)?.LastError;
            StatusText = $"Head tracking failed to start: {error ?? "unknown error"}";
        }
    }

    private void StopHeadTracking()
    {
        if (_headTrackingTimer is not null)
        {
            _headTrackingTimer.Tick -= HeadTrackingTimer_Tick;
            _headTrackingTimer.Stop();
        }

        _headTrackingService.Stop();
        _headTrackingService.ResetState();
        UpdateProviderStatus();
    }

    private void HeadTrackingTimer_Tick(object? sender, EventArgs e)
    {
        UpdateHeadTracking();
    }

    private void UpdateProviderStatus()
    {
        if (_currentProvider is null)
        {
            HeadTrackingProviderStatus = "Unavailable";
            return;
        }

        if (_currentProvider.IsTracking)
        {
            HeadTrackingProviderStatus = "Tracking";
        }
        else if (_currentProvider.IsAvailable)
        {
            HeadTrackingProviderStatus = "Ready";
        }
        else
        {
            HeadTrackingProviderStatus = "Error";
        }

        OnPropertyChanged(nameof(HeadTrackingProviderStatusColor));
    }

    // ─── Latency mode & diagnostics ──────────────────────────────────────

    public AudioLatencyMode SelectedLatencyMode
    {
        get => _selectedLatencyMode;
        set
        {
            if (SetProperty(ref _selectedLatencyMode, value))
            {
                _gameAudioService.LatencyMode = value;
                OnPropertyChanged(nameof(LatencyModeRequiresRestart));
                UpdateLatencyModeSafetyMessage();
                _saveAction?.Invoke();
            }
        }
    }

    /// <summary>
    /// Always true — WaveOutEvent cannot be reconfigured while playing.
    /// Changing latency mode requires stopping and restarting capture.
    /// </summary>
    public bool LatencyModeRequiresRestart => true;

    public double DspP99Us
    {
        get => _dspP99Us;
        private set => SetProperty(ref _dspP99Us, value);
    }

    public double DspBudgetPercent
    {
        get => _dspBudgetPercent;
        private set => SetProperty(ref _dspBudgetPercent, value);
    }

    public long OverBudgetBlockCount
    {
        get => _overBudgetBlockCount;
        private set => SetProperty(ref _overBudgetBlockCount, value);
    }

    public string LatencyHealthStatus
    {
        get => _latencyHealthStatus;
        private set => SetProperty(ref _latencyHealthStatus, value);
    }

    public string LatencyHealthStatusColor
    {
        get
        {
            return _latencyHealthStatus switch
            {
                "Healthy" => "#22C55E",
                "Warning" => "#FBBF24",
                "Critical" => "#E85555",
                _ => "#6B7280"
            };
        }
    }

    public double EstimatedPipelineLatencyMs
    {
        get => _estimatedPipelineLatencyMs;
        private set => SetProperty(ref _estimatedPipelineLatencyMs, value);
    }

    public double ConfiguredOutputLatencyMs
    {
        get => _configuredOutputLatencyMs;
        private set => SetProperty(ref _configuredOutputLatencyMs, value);
    }

    public double DspP95Us
    {
        get => _dspP95Us;
        private set => SetProperty(ref _dspP95Us, value);
    }

    public double CaptureToDspLatencyMs
    {
        get => _captureToDspLatencyMs;
        private set => SetProperty(ref _captureToDspLatencyMs, value);
    }

    public long CaptureStarvationCount
    {
        get => _captureStarvationCount;
        private set => SetProperty(ref _captureStarvationCount, value);
    }

    public string LatencyModeSafetyMessage
    {
        get => _latencyModeSafetyMessage;
        private set => SetProperty(ref _latencyModeSafetyMessage, value);
    }

    public bool HasLatencyModeWarning => !string.IsNullOrEmpty(LatencyModeSafetyMessage);

    private void StartDiagnosticsTimer()
    {
        if (_diagnosticsTimer is not null) return;
        _diagnosticsTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250) // 4 Hz refresh
        };
        _diagnosticsTimer.Tick += DiagnosticsTimer_Tick;
        _diagnosticsTimer.Start();
    }

    private void StopDiagnosticsTimer()
    {
        if (_diagnosticsTimer is null) return;
        _diagnosticsTimer.Tick -= DiagnosticsTimer_Tick;
        _diagnosticsTimer.Stop();
        _diagnosticsTimer = null;
    }

    private void DiagnosticsTimer_Tick(object? sender, EventArgs e)
    {
        RefreshDiagnostics();
    }

    private void RefreshDiagnostics()
    {
        var monitor = _gameAudioService.ProcessingMonitor;
        if (monitor is null) return;

        var snapshot = monitor.GetSnapshot();
        var outputInfo = _gameAudioService.OutputLatencyInfo;
        var healthMonitor = _gameAudioService.HealthMonitor;
        var tsMonitor = _gameAudioService.TimestampMonitor;

        DspP95Us = snapshot.P95Us;
        DspP99Us = snapshot.P99Us;
        OverBudgetBlockCount = snapshot.OverBudgetBlockCount;

        // Calculate budget percentage
        if (snapshot.BlockDurationUs > 0)
            DspBudgetPercent = (snapshot.P99Us / snapshot.BlockDurationUs) * 100.0;
        else
            DspBudgetPercent = 0;

        // Update health monitor with hysteresis
        if (healthMonitor is not null)
        {
            healthMonitor.UpdateState(new AudioLatencySnapshot
            {
                DspBudgetPercent = DspBudgetPercent,
                OverBudgetBlockCount = snapshot.OverBudgetBlockCount,
                HealthStatus = snapshot.OverBudgetBlockCount > 0
                    ? AudioHealthStatus.Critical
                    : DspBudgetPercent > 80
                        ? AudioHealthStatus.Warning
                        : AudioHealthStatus.Healthy
            });
            LatencyHealthStatus = healthMonitor.CurrentState switch
            {
                AudioHealthStatus.Healthy => "Healthy",
                AudioHealthStatus.Warning => "Warning",
                AudioHealthStatus.Critical => "Critical",
                _ => "Unavailable"
            };
        }
        else
        {
            // Fallback without hysteresis
            if (snapshot.OverBudgetBlockCount > 0)
                LatencyHealthStatus = "Critical";
            else if (DspBudgetPercent > 80)
                LatencyHealthStatus = "Warning";
            else
                LatencyHealthStatus = "Healthy";
        }

        OnPropertyChanged(nameof(LatencyHealthStatusColor));

        // Timestamp data
        if (tsMonitor is not null)
        {
            var tsSnapshot = tsMonitor.GetSnapshot();
            CaptureToDspLatencyMs = tsSnapshot.CaptureToDspAvgMs;
        }

        // Capture starvation tracking
        CaptureStarvationCount = monitor.TotalCount > 0
            ? Math.Max(0, monitor.TotalCount - snapshot.MeasurementCount)
            : 0;

        // Estimated pipeline latency (application only, honest label)
        var captureMs = Services.Diagnostics.AudioLatencyConfiguration.CaptureBufferMs;
        var outputMs = outputInfo?.EstimatedOutputBufferLatencyMs ?? 0;
        var dspContributionMs = snapshot.P99Us / 1000.0;
        EstimatedPipelineLatencyMs = captureMs + dspContributionMs + outputMs;
        ConfiguredOutputLatencyMs = outputMs;
    }

    private void UpdateLatencyModeSafetyMessage()
    {
        var warnings = Services.Diagnostics.AudioLatencyConfiguration.GetSafetyWarnings(_selectedLatencyMode);
        LatencyModeSafetyMessage = warnings.Length > 0
            ? string.Join(" ", warnings)
            : string.Empty;
        OnPropertyChanged(nameof(HasLatencyModeWarning));
    }

    private void UpdateHrtfDirection()
    {
        if (!IsHrtfEnabled) return;
        var hrtf = _gameAudioService.Enhancement.HrtfSpatializer;
        if (hrtf?.ActiveProfile is not null)
            hrtf.SetDirection(_hrtfAzimuth, _hrtfElevation);
    }

    private void PersistHrtfDirection()
    {
        if (_settings is null) return;
        _settings.HrtfAzimuth = _hrtfAzimuth;
        _settings.HrtfElevation = _hrtfElevation;
    }

    private void PersistHrtfSpatialMix()
    {
        if (_settings is null) return;
        _settings.HrtfSpatialMix = _hrtfSpatialMixPct / 100.0;
    }

    public bool CanDeleteHrtfProfile =>
        SelectedHrtfProfile is not null
        && !PresetProfileIds.Contains(SelectedHrtfProfile.Id);

    private async Task ImportHrtfProfileAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import HRTF SOFA Profile",
            Filter = "SOFA files (*.sofa)|*.sofa|All files (*.*)|*.*",
            FilterIndex = 1,
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
            return;

        var filePath = dialog.FileName;
        ErrorText = string.Empty;
        StatusText = "Importing SOFA profile...";

        try
        {
            var result = await Task.Run(() => _sofaLoader.Load(filePath));

            if (!result.Success)
            {
                ErrorText = result.ErrorMessage ?? "Unknown import error.";
                StatusText = "Import failed.";
                return;
            }

            var profile = result.Profile!;
            _profileStore.Save(profile);
            AvailableHrtfProfiles.Add(profile);
            SelectedHrtfProfile = profile;
            StatusText = $"Imported: {profile.Name} ({result.DirectionsLoaded} directions, {result.IrLength} taps)";
        }
        catch (Exception ex)
        {
            ErrorText = $"Import error: {ex.Message}";
            StatusText = "Import failed.";
        }
    }

    private void DeleteHrtfProfile()
    {
        if (SelectedHrtfProfile is null) return;
        if (PresetProfileIds.Contains(SelectedHrtfProfile.Id)) return;

        var removedId = SelectedHrtfProfile.Id;
        var removedName = SelectedHrtfProfile.Name;

        // Find and remove from collection
        var index = AvailableHrtfProfiles.IndexOf(SelectedHrtfProfile);
        if (index < 0) return;

        AvailableHrtfProfiles.RemoveAt(index);

        // If this was the active profile, fall back to None
        var wasActive = string.Equals(_settings?.ActiveHrtfProfileId, removedId, StringComparison.OrdinalIgnoreCase);

        // Select the nearest available profile
        SelectedHrtfProfile = AvailableHrtfProfiles.Count > 0
            ? AvailableHrtfProfiles[Math.Min(index, AvailableHrtfProfiles.Count - 1)]
            : null;

        // Persist deletion
        try { _profileStore.Delete(removedId); } catch { }

        if (wasActive && _settings is not null)
            _settings.ActiveHrtfProfileId = SelectedHrtfProfile?.Id ?? string.Empty;

        OnPropertyChanged(nameof(CanDeleteHrtfProfile));
        StatusText = $"Deleted: {removedName}";
        try { _saveAction?.Invoke(); } catch { }
    }

    // ─── ArtTune tune library ────────────────────────────────────────────

    private void LoadCachedArtTuneLibrary()
    {
        try
        {
            var version = _artTuneLibrary.LoadFromCache();
            IntegrateArtTuneLibrary();
            if (_settings is not null && !string.IsNullOrEmpty(version) && string.IsNullOrEmpty(_settings.TuneLibraryVersion))
                _settings.TuneLibraryVersion = version;
            TuneLibraryStatus = string.IsNullOrEmpty(version)
                ? "No cached tune library"
                : $"Tune library v {version}";
        }
        catch
        {
            TuneLibraryStatus = "Tune library unavailable";
        }
    }

    private void IntegrateArtTuneLibrary()
    {
        foreach (var hp in _artTuneLibrary.HeadphoneProfiles)
            AddUniqueHeadphoneProfile(hp);
        foreach (var gp in _artTuneLibrary.GamingProfiles)
            AddUniqueGamingProfile(gp);

        if (_artTuneLibrary.HrtfProfile is { } hrtf
            && AvailableHrtfProfiles.All(p => !string.Equals(p.Id, hrtf.Id, StringComparison.OrdinalIgnoreCase)))
        {
            AvailableHrtfProfiles.Add(hrtf);
        }
    }

    private void AddUniqueHeadphoneProfile(HeadphoneProfile profile)
    {
        if (AvailableHeadphoneProfiles.Any(p => string.Equals(p.Id, profile.Id, StringComparison.OrdinalIgnoreCase)))
            return;
        AvailableHeadphoneProfiles.Add(profile);
    }

    private void AddUniqueGamingProfile(GamingProfile profile)
    {
        if (AvailableProfiles.Any(p => string.Equals(p.Id, profile.Id, StringComparison.OrdinalIgnoreCase)))
            return;
        AvailableProfiles.Add(profile);
    }

    private async Task UpdateTuneLibraryAsync()
    {
        if (IsTuneLibraryUpdating)
            return;

        IsTuneLibraryUpdating = true;
        try
        {
            TuneLibraryStatus = "Checking for tune library updates…";
            StatusText = "Checking ArtTuneDB…";

            var result = await Task.Run(() => _artTuneLibrary.UpdateAsync(progress => StatusText = progress));

            if (!string.IsNullOrEmpty(result.Error))
            {
                ErrorText = result.Error;
                StatusText = "Tune library update failed.";
                TuneLibraryStatus = "Update failed";
                return;
            }

            if (result.Updated)
            {
                ReplaceArtTuneProfiles();
                if (_settings is not null)
                {
                    _settings.TuneLibraryVersion = result.Version;
                    try { _saveAction?.Invoke(); } catch { }
                }
                StatusText = result.Message;
                TuneLibraryStatus = $"Updated (v {result.Version})";
            }
            else if (result.IsCurrent)
            {
                StatusText = result.Message;
                TuneLibraryStatus = $"Current (v {result.Version})";
            }
            else
            {
                StatusText = result.Message;
                TuneLibraryStatus = string.IsNullOrEmpty(result.Error) ? "No changes" : "Update failed";
            }
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
            StatusText = "Tune library update failed.";
            TuneLibraryStatus = "Update failed";
        }
        finally
        {
            IsTuneLibraryUpdating = false;
        }
    }

    private void ReplaceArtTuneProfiles()
    {
        var prevGamingId = SelectedProfile?.Id;
        var prevHpId = SelectedHeadphoneProfile?.Id;
        var prevHrtfId = SelectedHrtfProfile?.Id;

        for (int i = AvailableProfiles.Count - 1; i >= 0; i--)
        {
            if (AvailableProfiles[i].Id.StartsWith("arttune-", StringComparison.OrdinalIgnoreCase))
                AvailableProfiles.RemoveAt(i);
        }

        for (int i = AvailableHeadphoneProfiles.Count - 1; i >= 0; i--)
        {
            if (AvailableHeadphoneProfiles[i].Id.StartsWith("arttune-", StringComparison.OrdinalIgnoreCase))
                AvailableHeadphoneProfiles.RemoveAt(i);
        }

        for (int i = AvailableHrtfProfiles.Count - 1; i >= 0; i--)
        {
            if (AvailableHrtfProfiles[i].Id.StartsWith("arttune-", StringComparison.OrdinalIgnoreCase))
                AvailableHrtfProfiles.RemoveAt(i);
        }

        IntegrateArtTuneLibrary();

        if (!string.IsNullOrEmpty(prevGamingId))
            SelectedProfile = prevGamingId is string prevGame
                ? AvailableProfiles.FirstOrDefault(p => string.Equals(p.Id, prevGame, StringComparison.OrdinalIgnoreCase))
                  ?? AvailableProfiles.FirstOrDefault()
                : AvailableProfiles.FirstOrDefault();

        if (!string.IsNullOrEmpty(prevHpId) && prevHpId is string prevHp)
            SelectedHeadphoneProfile = AvailableHeadphoneProfiles.FirstOrDefault(p => string.Equals(p.Id, prevHp, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(prevHrtfId) && prevHrtfId is string prevHrtf)
            SelectedHrtfProfile = AvailableHrtfProfiles.FirstOrDefault(p => string.Equals(p.Id, prevHrtf, StringComparison.OrdinalIgnoreCase));
    }

    private void RestoreSettings()
    {
        if (_settings is null) return;

        try
        {
            // HRTF toggle + profile
            IsHrtfEnabled = _settings.HrtfEnabled;

            var savedHrtfId = _settings.ActiveHrtfProfileId;
            if (!string.IsNullOrEmpty(savedHrtfId))
            {
                var match = AvailableHrtfProfiles
                    .FirstOrDefault(p => string.Equals(p.Id, savedHrtfId, StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                    SelectedHrtfProfile = match;
                else
                    SelectedHrtfProfile = AvailableHrtfProfiles.FirstOrDefault(); // fallback to None
            }

            // HRTF direction + mix (applied after profile is loaded)
            _hrtfAzimuth = Math.Clamp(_settings.HrtfAzimuth, -180.0, 180.0);
            _hrtfElevation = Math.Clamp(_settings.HrtfElevation, -90.0, 90.0);
            _hrtfSpatialMixPct = Math.Clamp(_settings.HrtfSpatialMix * 100.0, 0.0, 100.0);
            OnPropertyChanged(nameof(HrtfAzimuth));
            OnPropertyChanged(nameof(HrtfElevation));
            OnPropertyChanged(nameof(HrtfSpatialMixPct));

            // Headphone EQ toggle + profile
            IsHeadphoneEqEnabled = _settings.HeadphoneEqEnabled;

            var savedHpId = _settings.ActiveHeadphoneProfileId;
            if (!string.IsNullOrEmpty(savedHpId))
            {
                var match = AvailableHeadphoneProfiles
                    .FirstOrDefault(p => string.Equals(p.Id, savedHpId, StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                    SelectedHeadphoneProfile = match;
            }

            // Head tracking
            IsHrtfHeadTrackingEnabled = _settings.HrtfHeadTrackingEnabled;

            // Latency mode
            SelectedLatencyMode = _settings.AudioLatencyMode;
        }
        catch
        {
            StatusText = "Some settings could not be restored.";
        }
    }

    public void RefreshProcesses()
    {
        RunningProcesses.Clear();
        _processPidMap.Clear();

        try
        {
            var processes = Process.GetProcesses()
                .Where(p =>
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(p.MainWindowTitle)) return false;
                        if (ExcludedProcessNames.Contains(p.ProcessName)) return false;
                        return true;
                    }
                    catch { return false; }
                })
                .OrderBy(p => p.ProcessName)
                .ToList();

            foreach (var process in processes)
            {
                try
                {
                    var path = string.Empty;
                    try { path = process.MainModule?.FileName ?? string.Empty; } catch { }

                    var info = new GameProcessInfo
                    {
                        ProcessName = process.ProcessName,
                        ExecutableName = System.IO.Path.GetFileName(path),
                        DisplayName = process.MainWindowTitle,
                        ExecutablePath = path
                    };

                    RunningProcesses.Add(info);
                    _processPidMap[info.DisplayName] = (uint)process.Id;
                }
                catch { }
                finally
                {
                    process.Dispose();
                }
            }

            StatusText = $"Found {RunningProcesses.Count} process(es)";
        }
        catch (Exception ex)
        {
            StatusText = $"Error listing processes: {ex.Message}";
        }
    }

    private void StartCapture()
    {
        if (SelectedProcess is null || !IsEnabled) return;

        Services.ActionLog.Instance.Action("Gaming", $"StartCapture: process='{SelectedProcess?.DisplayName}'");

        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 20348))
        {
            ErrorText = "Application Loopback requires Windows 10 build 20348+.";
            StatusText = "Capture failed: Windows version too old.";
            return;
        }

        if (!_processPidMap.TryGetValue(SelectedProcess.DisplayName, out var pid))
        {
            StatusText = "Selected process not found. Click Refresh.";
            Services.ActionLog.Instance.Error("Gaming", $"PID not found for '{SelectedProcess.DisplayName}'");
            return;
        }

        try
        {
            ErrorText = string.Empty;
            _gameAudioService.LatencyMode = _selectedLatencyMode;
            _gameAudioService.StartCapture(pid);
            IsCapturing = true;
            Services.ActionLog.Instance.Action("Gaming", $"StartCapture OK: pid={pid}");
        }
        catch (Exception ex)
        {
            Services.ActionLog.Instance.Error("Gaming", $"StartCapture failed: {ex}");
            ErrorText = ex.Message;
            StatusText = $"Capture failed: {ex.Message}";
            IsCapturing = false;
        }
    }

    internal void StopCapture()
    {
        if (!IsCapturing) return;

        try
        {
            _gameAudioService.StopCapture();
            IsCapturing = false;
            StatusText = "Capture stopped";
            ErrorText = string.Empty;
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
            StatusText = $"Stop failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Additive: resets the whole gaming audio system back to a "fresh start"
    /// state — stops any active capture, restores every suppressed session
    /// (beyond just the current process), and resets the gaming DSP chain to
    /// its out-of-box bypassed defaults. Only adds the missing glue; all
    /// existing method bodies are untouched.
    /// </summary>
    private void ResetSystemToDefaults()
    {
        try
        {
            if (IsCapturing)
            {
                StopCapture();
            }
            else
            {
                _gameAudioService.RestoreAllSessions();
                _gameAudioService.Enhancement.ResetToDefaults();
                StatusText = "System restored to default state";
                ErrorText = string.Empty;
            }
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
            StatusText = $"Reset failed: {ex.Message}";
        }
    }

    // ─── Footstep auto setup (mirrors the original ArtTune workflow) ───────

    /// <summary>
    /// Picks the ArtTuneDB game tune that matches the selected process and enables
    /// the EAC HRTF spatializer, so footsteps are EQ-boosted and spatialized the
    /// same way the original ArtTune app does. Manual selections are respected.
    /// </summary>
    private void AutoApplyFootstepEnhancements()
    {
        if (SelectedProcess is null)
            return;

        AutoMatchGameProfile();

        if (SelectedProfile?.Id.StartsWith("arttune-", StringComparison.OrdinalIgnoreCase) != true)
            return;

        if (!IsHrtfEnabled)
        {
            var eac = AvailableHrtfProfiles.FirstOrDefault(p =>
                string.Equals(p.Id, ArtTuneLibraryService.HrtfProfileId, StringComparison.OrdinalIgnoreCase));
            SelectedHrtfProfile = eac ?? AvailableHrtfProfiles.FirstOrDefault(p =>
                !string.Equals(p.Id, "none", StringComparison.OrdinalIgnoreCase));
            IsHrtfEnabled = true;
        }
    }

    /// <summary>
    /// Selects the matching game tune for the selected process. Only acts when
    /// the user has not already chosen an ArtTune tune, so manual picks win.
    /// </summary>
    private void AutoMatchGameProfile()
    {
        var lookFor = SelectedProcess is null ? null :
            $"{SelectedProcess.DisplayName} {SelectedProcess.ExecutableName}".ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(lookFor))
            return;

        string? matchedGame = null;
        foreach (var (game, keywords) in GameKeywords)
        {
            if (keywords.Any(k => lookFor.Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                matchedGame = game;
                break;
            }
        }
        if (matchedGame is null)
            return;

        var candidates = AvailableProfiles
            .Where(p => p.Id.StartsWith("arttune-", StringComparison.OrdinalIgnoreCase))
            .Where(p => string.Equals(p.Category, matchedGame, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (candidates.Count == 0)
            return;

        // Respect an existing manual pick for this game
        if (SelectedProfile is { } current
            && current.Id.StartsWith("arttune-", StringComparison.OrdinalIgnoreCase)
            && string.Equals(current.Category, matchedGame, StringComparison.OrdinalIgnoreCase))
            return;

        var best = candidates.FirstOrDefault(p => p.Name.Contains("post", StringComparison.OrdinalIgnoreCase))
                   ?? candidates[0];

        SelectedProfile = best;
    }

    private void OnCaptureStarted(object? sender, EventArgs e)
    {
        IsCapturing = true;
        AutoApplyFootstepEnhancements();
        StatusText = $"Capturing: {SelectedProcess?.DisplayName ?? "unknown"}";
        StartDiagnosticsTimer();
    }

    private void OnCaptureStopped(object? sender, string message)
    {
        IsCapturing = false;
        StopDiagnosticsTimer();
        StatusText = string.IsNullOrEmpty(message) ? "Capture stopped" : message;
    }

    private void OnCaptureError(object? sender, Exception ex)
    {
        IsCapturing = false;
        ErrorText = ex.Message;
        StatusText = $"Error: {ex.Message}";
    }

    // ─── ArtTune stack (one-button installer, mirrors ArtTuneDB) ─────────

    public RelayCommand RefreshArtTuneStackCommand { get; private set; } = null!;
    public RelayCommand OpenArtTuneGuideCommand { get; private set; } = null!;
    public AsyncRelayCommand InstallArtTuneStackCommand { get; private set; } = null!;
    public AsyncRelayCommand RollbackArtTuneStackCommand { get; private set; } = null!;
    public AsyncRelayCommand ApplyArtTuneTuneCommand { get; private set; } = null!;

    public ObservableCollection<string> ArtTuneVersions { get; } = new();
    public ObservableCollection<string> ArtTuneSixteenChOptions { get; } = new();

    public ArtTuneStackState StackState
    {
        get => _artTuneStackState;
        private set
        {
            if (SetProperty(ref _artTuneStackState, value))
            {
                OnPropertyChanged(nameof(StackReady));
                OnPropertyChanged(nameof(MissingComponents));
                OnPropertyChanged(nameof(ArtTuneStatusText));
                OnPropertyChanged(nameof(StackStateDescription));
            }
        }
    }

    public bool IsArtTuneBusy
    {
        get => _isArtTuneBusy;
        private set
        {
            if (SetProperty(ref _isArtTuneBusy, value))
            {
                InstallArtTuneStackCommand.RaiseCanExecuteChanged();
                ApplyArtTuneTuneCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasArtTuneSixteenCh
    {
        get => ArtTuneSixteenChOptions.Count > 0;
        set => OnPropertyChanged(nameof(HasArtTuneSixteenCh));
    }

    public bool HasArtTuneLog
    {
        get => !string.IsNullOrWhiteSpace(ArtTuneRunLog);
        set => OnPropertyChanged(nameof(HasArtTuneLog));
    }

    public string ArtTuneRunLog
    {
        get => _artTuneRunLog;
        private set
        {
            if (SetProperty(ref _artTuneRunLog, value))
                OnPropertyChanged(nameof(HasArtTuneLog));
        }
    }

    public bool StackReady => StackState.AllCoreInstalled;

    public IReadOnlyList<string> MissingComponents => StackState.MissingComponents;

    public string StackStateDescription => StackReady
        ? "Art Tune stack installed — pick a game + version and hit Apply."
        : "Art Tune stack not installed. Run one-click install to match ArtTuneDB.";

    public string ArtTuneStatusText
    {
        get
        {
            if (StackReady)
            {
                var v = StackState.LibraryVersion;
                return StackState.EndpointsRenamed
                    ? $"Ready ({v}, endpoints named Art Tune / Art Tune +)"
                    : $"Ready ({v})";
            }
            return StackState.MissingComponents.Count == 0
                ? "Detecting…"
                : $"Missing: {string.Join(", ", StackState.MissingComponents)}";
        }
    }

    // ─── Live tuning verification (is the tuning actually working) ─────────

    public string TuningHealthText
    {
        get => _tuningHealthText;
        private set => SetProperty(ref _tuningHealthText, value);
    }

    public System.Windows.Media.Brush TuningHealthColor
    {
        get => _tuningHealthColor;
        private set => SetProperty(ref _tuningHealthColor, value);
    }

    public string TuningHealthDetail
    {
        get => _tuningHealthDetail;
        private set => SetProperty(ref _tuningHealthDetail, value);
    }

    private void StartArtTuneHealthTimer()
    {
        if (_artTuneHealthTimer is not null) return;
        try
        {
            if (System.Windows.Application.Current is null) return; // headless tests
            _artTuneHealthTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _artTuneHealthTimer.Tick += (_, _) => VerifyArtTuneTuning();
            _artTuneHealthTimer.Start();
        }
        catch { /* timer unavailable - static checks still run */ }
        VerifyArtTuneTuning();
    }

    private void VerifyArtTuneTuning()
    {
        string? expected = null;
        if (_settings?.ActiveGamingProfileId is { Length: > 0 } profileId)
            expected = profileId.Replace('\\', '/').Trim();
        else
        {
            var touch = SplitVersion(SelectedArtTuneVersion);
            if (touch is not null) expected = $"{touch.Value.Game}/{touch.Value.Version}";
        }

        ArtTuneTuningVerification v;
        try { v = ArtTuneVerifier.VerifyTuning(expected); }
        catch { return; }

        if (v.Health == _artTuneTuning.Health && v.Summary == _artTuneTuning.Summary) return;
        _artTuneTuning = v;

        TuningHealthText = v.Health switch
        {
            ArtTuneTuningHealth.Active => "TUNING ACTIVE",
            ArtTuneTuningHealth.Partial => "TUNING PARTIAL",
            _ => "TUNING NOT ACTIVE"
        };
        TuningHealthColor = v.Health switch
        {
            ArtTuneTuningHealth.Active => CreateColor(0x22, 0xC5, 0x5E),
            ArtTuneTuningHealth.Partial => CreateColor(0xF5, 0x9E, 0x0B),
            _ => CreateColor(0xE8, 0x55, 0x55)
        };
        TuningHealthDetail = v.Summary;
    }

    private static System.Windows.Media.Brush CreateColor(byte r, byte g, byte b) =>
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));

    public void RefreshTuningVerification() => VerifyArtTuneTuning();

    public string SelectedArtTuneVersion
    {
        get => _selectedArtTuneVersion;
        set
        {
            if (SetProperty(ref _selectedArtTuneVersion, value))
                PopulateSixteenChOptions();
        }
    }

    public string SelectedArtTuneSixteenCh
    {
        get => _selectedArtTuneSixteenCh;
        set => SetProperty(ref _selectedArtTuneSixteenCh, value);
    }

    private void PopulateSixteenChOptions()
    {
        ArtTuneSixteenChOptions.Clear();
        SelectedArtTuneSixteenCh = string.Empty;
        var touch = SplitVersion(_selectedArtTuneVersion);
        if (touch is null) return;
        var match = _installedTuneVersions
            .FirstOrDefault(v => v.Game == touch.Value.Game && v.Version == touch.Value.Version);
        if (match is null) return;
        foreach (var f in match.SixteenChFiles)
            ArtTuneSixteenChOptions.Add(f);
        OnPropertyChanged(nameof(HasArtTuneSixteenCh));
    }

    private void RefreshArtTuneStack()
    {
        StackState = ArtTuneStackService.Detect();
        ArtTuneVersions.Clear();
        _installedTuneVersions = ArtTuneStackService.EnumerateLibrary();
        foreach (var v in _installedTuneVersions)
            ArtTuneVersions.Add($"{v.Game}  {v.Version}");
        OnPropertyChanged(nameof(HasArtTuneSixteenCh));
        VerifyArtTuneTuning();
    }

    private async Task InstallArtTuneStackAsync()
    {
        if (IsArtTuneBusy) return;
        IsArtTuneBusy = true;
        ArtTuneRunLog = string.Empty;
        try
        {
            StatusText = "Installing Art Tune stack…";
            var progress = new Progress<string>(line =>
            {
                if (string.IsNullOrWhiteSpace(line)) return;
                ArtTuneRunLog += line + Environment.NewLine;
                StatusText = line;
            });
            var ok = await _artTuneStack.InstallStackAsync(progress);
            StatusText = ok ? "Art Tune stack install finished." : "Art Tune stack install reported failures.";
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
            StatusText = "Art Tune stack install failed.";
        }
        finally
        {
            IsArtTuneBusy = false;
            RefreshArtTuneStack();
        }
    }

    private async Task RollbackArtTuneStackAsync()
    {
        if (IsArtTuneBusy) return;
        IsArtTuneBusy = true;
        ArtTuneRunLog = string.Empty;
        try
        {
            StatusText = "Uninstalling Art Tune stack…";
            var progress = new Progress<string>(line =>
            {
                if (string.IsNullOrWhiteSpace(line)) return;
                ArtTuneRunLog += line + Environment.NewLine;
                StatusText = line;
            });
            var ok = await _artTuneStack.UninstallEverythingAsync(progress);
            StatusText = ok ? "Art Tune stack rollback finished." : "Art Tune stack rollback reported failures.";
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
            StatusText = "Art Tune stack rollback failed.";
        }
        finally
        {
            IsArtTuneBusy = false;
            RefreshArtTuneStack();
        }
    }

    private async Task ApplyArtTuneTuneAsync()
    {
        if (IsArtTuneBusy) return;
        var touch = SplitVersion(SelectedArtTuneVersion);
        if (touch is null)
        {
            ErrorText = "Select a game + version first.";
            return;
        }
        IsArtTuneBusy = true;
        ArtTuneRunLog = string.Empty;
        try
        {
            var (game, version) = touch.Value;
            var match = _installedTuneVersions
                .FirstOrDefault(v => v.Game == game && v.Version == version);
            var leqHint = match?.LeqReleaseTimeHint ?? 0;
            var sixteenCh = string.IsNullOrWhiteSpace(SelectedArtTuneSixteenCh)
                ? match?.SixteenChFiles.FirstOrDefault()
                : SelectedArtTuneSixteenCh;

            StatusText = $"Applying {game} {version}…";
            var progress = new Progress<string>(line =>
            {
                if (string.IsNullOrWhiteSpace(line)) return;
                ArtTuneRunLog += line + Environment.NewLine;
                StatusText = line;
            });

            var ok = await _artTuneStack.ApplyTuneAsync(
                game, version,
                sixteenChFile: sixteenCh,
                eqFile: match?.EqFile,
                leqReleaseTime: leqHint,
                progress: progress);

            if (_settings is not null)
            {
                _settings.ActiveGamingProfileId = $"{game}/{version}";
                try { _saveAction?.Invoke(); } catch { }
            }
            StatusText = ok
                ? $"Applied {game} {version} — config.txt + LEQ set."
                : $"Applying {game} {version} reported failures.";
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
            StatusText = "Apply tune failed.";
        }
        finally
        {
            IsArtTuneBusy = false;
            VerifyArtTuneTuning();
        }
    }

    private static (string Game, string Version)? SplitVersion(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var parts = value.Split(new[] { "  " }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) return null;
        return (parts[0].Trim(), parts[1].Trim());
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _gameAudioService.CaptureStarted -= OnCaptureStarted;
        _gameAudioService.CaptureStopped -= OnCaptureStopped;
        _gameAudioService.CaptureError -= OnCaptureError;

        StopDiagnosticsTimer();
        StopHeadTracking();
        _headTrackingTimer = null;
        _currentProvider?.Dispose();
        _currentProvider = null;

        if (_artTuneHealthTimer is not null)
        {
            try { _artTuneHealthTimer.Stop(); } catch { }
            _artTuneHealthTimer = null;
        }

        _headTrackingService.Dispose();

        if (IsCapturing)
        {
            try { _gameAudioService.StopCapture(); } catch { }
        }

        _gameAudioService.Dispose();
    }
}
