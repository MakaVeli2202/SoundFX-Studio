namespace SoundFXStudio.Services.Diagnostics;

/// <summary>
/// Production audio health monitor with hysteresis-based state machine.
///
/// Health states and transitions:
///   Healthy → Warning: when DSP P95 > 70% of block budget for > 5 seconds
///   Warning → Healthy: when DSP P95 < 50% of block budget for > 10 seconds
///   Warning → Critical: when any deadline miss detected OR device failure
///   Critical → Warning: when no deadline misses for > 30 seconds AND device OK
///   Any → Unavailable: when device not active
///   Unavailable → Healthy: when device becomes active and pipeline healthy
///
/// Hysteresis prevents oscillation between states when values hover near thresholds.
/// </summary>
public sealed class AudioProductionHealthMonitor
{
    private const double WarningThresholdPercent = 70.0;
    private const double HealthyThresholdPercent = 50.0;

    // Hysteresis durations in seconds
    private const int HealthyToWarningSeconds = 5;
    private const int WarningToHealthySeconds = 10;
    private const int WarningToCriticalSeconds = 0; // Immediate on deadline miss
    private const int CriticalToWarningSeconds = 30;

    private AudioHealthStatus _currentState = AudioHealthStatus.Unavailable;
    private DateTime _lastStateChange = DateTime.UtcNow;
    private DateTime _warningEnteredAt;
    private DateTime _criticalEnteredAt;

    // Counters for hysteresis
    private long _consecutiveOverBudgetBlocks;
    private long _consecutiveHealthyBlocks;
    private long _deadlineMissCount;
    private bool _deviceFailureDetected;

    public AudioHealthStatus CurrentState => _currentState;
    public DateTime LastStateChange => _lastStateChange;
    public TimeSpan TimeInCurrentState => DateTime.UtcNow - _lastStateChange;

    /// <summary>Total deadline misses detected since last reset.</summary>
    public long DeadlineMissCount => _deadlineMissCount;

    /// <summary>Whether a device failure has been detected.</summary>
    public bool DeviceFailureDetected => _deviceFailureDetected;

    /// <summary>
    /// Updates the health state based on new DSP measurement.
    /// Call this after each AudioLatencySnapshot is generated.
    /// </summary>
    /// <param name="snapshot">Current latency snapshot.</param>
    public void UpdateState(AudioLatencySnapshot snapshot)
    {
        var now = DateTime.UtcNow;

        // Check for device failure
        if (snapshot.HealthStatus == AudioHealthStatus.Unavailable)
        {
            _deviceFailureDetected = true;
            TransitionTo(AudioHealthStatus.Unavailable, now);
            return;
        }

        // Check for deadline miss
        if (snapshot.OverBudgetBlockCount > 0)
        {
            _deadlineMissCount += snapshot.OverBudgetBlockCount;
            _consecutiveOverBudgetBlocks++;
            _consecutiveHealthyBlocks = 0;

            if (_currentState != AudioHealthStatus.Critical)
            {
                TransitionTo(AudioHealthStatus.Critical, now);
            }
            return;
        }

        // DSP budget analysis
        if (snapshot.DspBudgetPercent > WarningThresholdPercent)
        {
            _consecutiveOverBudgetBlocks++;
            _consecutiveHealthyBlocks = 0;

            if (_currentState == AudioHealthStatus.Unavailable)
            {
                // Device became active with high budget — start at Healthy, let hysteresis escalate
                TransitionTo(AudioHealthStatus.Healthy, now);
            }
            else if (_currentState == AudioHealthStatus.Healthy)
            {
                if (_consecutiveOverBudgetBlocks * 0.25 >= HealthyToWarningSeconds) // 0.25s per block at 48kHz/1024
                {
                    TransitionTo(AudioHealthStatus.Warning, now);
                }
            }
            else if (_currentState == AudioHealthStatus.Critical)
            {
                // Check if we can recover to Warning
                if ((now - _criticalEnteredAt).TotalSeconds >= CriticalToWarningSeconds)
                {
                    TransitionTo(AudioHealthStatus.Warning, now);
                }
            }
        }
        else if (snapshot.DspBudgetPercent < HealthyThresholdPercent)
        {
            _consecutiveHealthyBlocks++;
            _consecutiveOverBudgetBlocks = 0;

            if (_currentState == AudioHealthStatus.Warning)
            {
                if (_consecutiveHealthyBlocks * 0.25 >= WarningToHealthySeconds)
                {
                    TransitionTo(AudioHealthStatus.Healthy, now);
                }
            }
            else if (_currentState == AudioHealthStatus.Critical)
            {
                if ((now - _criticalEnteredAt).TotalSeconds >= CriticalToWarningSeconds)
                {
                    TransitionTo(AudioHealthStatus.Warning, now);
                }
            }
            else if (_currentState == AudioHealthStatus.Unavailable)
            {
                TransitionTo(AudioHealthStatus.Healthy, now);
            }
        }
    }

    /// <summary>
    /// Forces the state to Unavailable (e.g., when audio pipeline stops).
    /// </summary>
    public void MarkUnavailable()
    {
        _deviceFailureDetected = false;
        TransitionTo(AudioHealthStatus.Unavailable, DateTime.UtcNow);
    }

    /// <summary>
    /// Resets all counters and state. Call when audio pipeline restarts.
    /// </summary>
    public void Reset()
    {
        _currentState = AudioHealthStatus.Unavailable;
        _lastStateChange = DateTime.UtcNow;
        _consecutiveOverBudgetBlocks = 0;
        _consecutiveHealthyBlocks = 0;
        _deadlineMissCount = 0;
        _deviceFailureDetected = false;
    }

    /// <summary>
    /// Gets human-readable description of current health state.
    /// </summary>
    public string GetStatusDescription()
    {
        return _currentState switch
        {
            AudioHealthStatus.Healthy => "DSP pipeline healthy",
            AudioHealthStatus.Warning => $"DSP approaching deadline ({_consecutiveOverBudgetBlocks} consecutive high blocks)",
            AudioHealthStatus.Critical => $"DSP deadline misses detected ({_deadlineMissCount} total)",
            AudioHealthStatus.Unavailable => "Audio pipeline not active",
            _ => "Unknown"
        };
    }

    private void TransitionTo(AudioHealthStatus newState, DateTime now)
    {
        if (_currentState == newState) return;

        _currentState = newState;
        _lastStateChange = now;

        if (newState == AudioHealthStatus.Warning)
        {
            _warningEnteredAt = now;
        }
        else if (newState == AudioHealthStatus.Critical)
        {
            _criticalEnteredAt = now;
        }
    }
}
