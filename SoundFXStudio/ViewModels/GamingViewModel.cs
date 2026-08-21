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
    private bool _disposed;
    private bool _isEnabled;
    private bool _isCapturing;
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
        RefreshProcessesCommand = new RelayCommand(_ => RefreshProcesses());
        ToggleEnableCommand = new RelayCommand(_ => IsEnabled = !IsEnabled);
        ToggleHeadphoneEqCommand = new RelayCommand(_ => IsHeadphoneEqEnabled = !IsHeadphoneEqEnabled);
        ToggleHrtfCommand = new RelayCommand(_ => IsHrtfEnabled = !IsHrtfEnabled);
        ImportHrtfProfileCommand = new AsyncRelayCommand(_ => ImportHrtfProfileAsync());
        DeleteHrtfProfileCommand = new RelayCommand(_ => DeleteHrtfProfile(), _ => CanDeleteHrtfProfile);
        CalibrateHeadTrackingCommand = new RelayCommand(_ => CalibrateHeadTracking(), _ => IsHrtfHeadTrackingEnabled);

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

        SelectedHrtfProfile = AvailableHrtfProfiles.FirstOrDefault();

        if (_settings is not null)
            RestoreSettings();

        try { InitializeHeadTrackingProvider(); } catch { /* provider init failed — head tracking unavailable */ }

        AvailableLatencyModes.Add(AudioLatencyMode.Safe);
        AvailableLatencyModes.Add(AudioLatencyMode.Balanced);
        AvailableLatencyModes.Add(AudioLatencyMode.LowLatency);

        RefreshProcesses();

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
                _setStatusAction?.Invoke(value);
        }
    }

    public string ErrorText
    {
        get => _errorText;
        set => SetProperty(ref _errorText, value);
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
    public RelayCommand ToggleEnableCommand { get; }
    public RelayCommand ToggleHeadphoneEqCommand { get; }
    public RelayCommand ToggleHrtfCommand { get; }
    public AsyncRelayCommand ImportHrtfProfileCommand { get; }
    public RelayCommand DeleteHrtfProfileCommand { get; }
    public RelayCommand CalibrateHeadTrackingCommand { get; }

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

        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 20348))
        {
            ErrorText = "Application Loopback requires Windows 10 build 20348+.";
            StatusText = "Capture failed: Windows version too old.";
            return;
        }

        if (!_processPidMap.TryGetValue(SelectedProcess.DisplayName, out var pid))
        {
            StatusText = "Selected process not found. Click Refresh.";
            return;
        }

        try
        {
            ErrorText = string.Empty;
            _gameAudioService.LatencyMode = _selectedLatencyMode;
            _gameAudioService.StartCapture(pid);
            IsCapturing = true;
        }
        catch (Exception ex)
        {
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

    private void OnCaptureStarted(object? sender, EventArgs e)
    {
        IsCapturing = true;
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

        _headTrackingService.Dispose();

        if (IsCapturing)
        {
            try { _gameAudioService.StopCapture(); } catch { }
        }

        _gameAudioService.Dispose();
    }
}
