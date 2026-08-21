using NAudio.Wave;
using SoundFXStudio.Models;
using SoundFXStudio.Services.Diagnostics;
using SoundFXStudio.Services.DSP;
using SoundFXStudio.Services.Interop;

namespace SoundFXStudio.Services;

/// <summary>
/// Captures audio from a specific game process via WASAPI Application Loopback,
/// suppresses the game's original audio session to prevent doubling,
/// routes the captured audio through GamingEnhancementService's DSP chain,
/// and outputs the processed audio to Voicemeeter via WaveOutEvent.
///
/// Audio path:
/// Game Process → Windows Audio Session (SUPPRESSED during capture)
///   → WASAPI Process Loopback (per-PID) → DSPChain (EQ + dynamics)
///   → WaveOutEvent → Voicemeeter Input → speakers/headphones
///
/// VoiceChangerService and Soundboard are completely independent and untouched.
/// </summary>
public sealed class GameAudioService : IDisposable
{
    private readonly AudioDeviceService _audioDeviceService = new();
    private readonly AudioSessionSuppressor _sessionSuppressor = new();
    private readonly object _lock = new();
    private ProcessLoopbackCapture? _capture;
    private WaveOutEvent? _waveOut;
    private EffectSampleProvider? _effectProvider;
    private WasapiCaptureSampleProvider? _sampleProvider;
    private bool _disposed;
    private bool _isCapturing;
    private uint _targetProcessId;
    private AudioLatencyMode _latencyMode = AudioLatencyMode.Balanced;
    private AudioProcessingMonitor? _processingMonitor;
    private AudioBlockTimestampMonitor? _timestampMonitor;
    private AudioProductionHealthMonitor? _healthMonitor;
    private AudioOutputLatencyInfo? _outputLatencyInfo;

    public bool IsCapturing
    {
        get { lock (_lock) return _isCapturing; }
    }

    public uint TargetProcessId
    {
        get { lock (_lock) return _targetProcessId; }
    }

    public GamingEnhancementService Enhancement { get; } = new();

    /// <summary>
    /// Current audio latency mode. Applied on next StartCapture().
    /// Changing while capturing requires a restart.
    /// </summary>
    public AudioLatencyMode LatencyMode
    {
        get { lock (_lock) return _latencyMode; }
        set { lock (_lock) _latencyMode = value; }
    }

    /// <summary>
    /// The DSP processing monitor. Active only while capturing.
    /// </summary>
    public AudioProcessingMonitor? ProcessingMonitor => _processingMonitor;

    /// <summary>
    /// Current output latency info. Null until first capture starts.
    /// </summary>
    public AudioOutputLatencyInfo? OutputLatencyInfo => _outputLatencyInfo;

    /// <summary>
    /// Timestamp monitor for block timing analysis. Active only while capturing.
    /// </summary>
    public AudioBlockTimestampMonitor? TimestampMonitor => _timestampMonitor;

    /// <summary>
    /// Production health monitor with hysteresis. Active only while capturing.
    /// </summary>
    public AudioProductionHealthMonitor? HealthMonitor => _healthMonitor;

    public event EventHandler? CaptureStarted;
    public event EventHandler<string>? CaptureStopped;
    public event EventHandler<Exception>? CaptureError;

    /// <summary>
    /// Starts capture for the given process ID.
    /// Auto-resolves the Voicemeeter Input device unless overridden.
    /// Suppresses the game's audio sessions to prevent doubling.
    /// </summary>
    public void StartCapture(uint processId, int outputDeviceIndex = -1)
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_isCapturing) StopCaptureUnlocked();

            if (outputDeviceIndex < 0)
                outputDeviceIndex = _audioDeviceService.ResolveVoicemeeterInputWaveOutIndex();

            if (outputDeviceIndex < 0)
                throw new InvalidOperationException(
                    "Voicemeeter Input device not found. Make sure Voicemeeter is running and has the 'VoiceMeeter Input' virtual device enabled.");

            _targetProcessId = processId;
        }

        try
        {
            int suppressed = _sessionSuppressor.SuppressProcess(processId);

            ProcessLoopbackCapture capture;
            WaveOutEvent waveOut;

            lock (_lock)
            {
                _capture = new ProcessLoopbackCapture(processId);
                _capture.RecordingStopped += OnCaptureStopped;

                _sampleProvider = new WasapiCaptureSampleProvider(_capture);

                _capture.StartRecording();

                var waveFormat = _capture.WaveFormat;
                Enhancement.SetSampleRate(waveFormat.SampleRate);

                _effectProvider = new EffectSampleProvider(_sampleProvider, Enhancement.Chain);

                var latencyConfig = AudioLatencyConfiguration.Resolve(_latencyMode);
                _waveOut = new WaveOutEvent
                {
                    DeviceNumber = outputDeviceIndex,
                    DesiredLatency = latencyConfig.DesiredLatencyMs,
                    NumberOfBuffers = latencyConfig.NumberOfBuffers
                };
                _waveOut.Init(_effectProvider);

                _outputLatencyInfo = AudioOutputLatencyInfo.FromConfiguration(
                    latencyConfig.DesiredLatencyMs,
                    latencyConfig.NumberOfBuffers,
                    waveFormat.SampleRate,
                    waveFormat.Channels);

                // Set up DSP processing monitor
                _processingMonitor = new AudioProcessingMonitor();
                // Block duration = per-buffer time = DesiredLatency / NumberOfBuffers
                var perBufferMs = (double)latencyConfig.DesiredLatencyMs / latencyConfig.NumberOfBuffers;
                _processingMonitor.SetBlockDurationUs(perBufferMs * 1000.0);
                _effectProvider.Monitor = _processingMonitor;

                // Set up timestamp monitor for block timing analysis
                _timestampMonitor = new AudioBlockTimestampMonitor();
                _effectProvider.TimestampMonitor = _timestampMonitor;

                // Set up production health monitor
                _healthMonitor = new AudioProductionHealthMonitor();

                capture = _capture;
                waveOut = _waveOut;

                _waveOut.Play();

                _isCapturing = true;
            }

            CaptureStarted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _sessionSuppressor.RestoreProcess(processId);
            CleanupCaptureResources();
            CaptureError?.Invoke(this, ex);
            throw;
        }
    }

    /// <summary>
    /// Stops capture, restores the game's original audio sessions.
    /// Thread-safe. Safe to call multiple times.
    /// </summary>
    public void StopCapture()
    {
        uint pid;
        lock (_lock)
        {
            if (!_isCapturing) return;
            pid = _targetProcessId;
            StopCaptureUnlocked();
        }

        _sessionSuppressor.RestoreProcess(pid);
        CaptureStopped?.Invoke(this, $"Process {pid} stopped");
    }

    private void StopCaptureUnlocked()
    {
        _isCapturing = false;
        CleanupCaptureResources();
    }

    private void OnCaptureStopped(object? sender, Exception? ex)
    {
        uint pid;
        bool wasCapturing;

        lock (_lock)
        {
            wasCapturing = _isCapturing;
            pid = _targetProcessId;
            if (wasCapturing)
                StopCaptureUnlocked();
        }

        if (ex is not null)
            CaptureError?.Invoke(this, ex);

        if (wasCapturing)
        {
            _sessionSuppressor.RestoreProcess(pid);
            CaptureStopped?.Invoke(this, "Capture stopped unexpectedly");
        }
    }

    public void SetOutputDevice(int deviceIndex)
    {
        uint pid;
        lock (_lock)
        {
            if (!_isCapturing) return;
            pid = _targetProcessId;
            StopCaptureUnlocked();
        }

        _sessionSuppressor.RestoreProcess(pid);

        try
        {
            StartCapture(pid, deviceIndex);
        }
        catch
        {
            CaptureStopped?.Invoke(this, $"Device change failed for process {pid}");
        }
    }

    private void CleanupCaptureResources()
    {
        _healthMonitor?.MarkUnavailable();

        if (_capture is not null)
        {
            _capture.RecordingStopped -= OnCaptureStopped;
            try { _capture.StopRecording(); } catch { }
            _capture.Dispose();
            _capture = null;
        }

        if (_waveOut is not null)
        {
            try { _waveOut.Stop(); } catch { }
            _waveOut.Dispose();
            _waveOut = null;
        }

        if (_effectProvider is not null)
        {
            _effectProvider.Monitor = null;
            _effectProvider.TimestampMonitor = null;
        }

        _processingMonitor = null;
        _timestampMonitor = null;
        _healthMonitor = null;
        _effectProvider = null;
        _sampleProvider = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        uint pid;
        lock (_lock)
        {
            pid = _targetProcessId;
            StopCaptureUnlocked();
        }

        _sessionSuppressor.RestoreProcess(pid);
        _sessionSuppressor.Dispose();
    }

    private sealed class WasapiCaptureSampleProvider : ISampleProvider
    {
        private readonly ProcessLoopbackCapture _capture;
        private readonly object _lock = new();
        private float[] _buffer = Array.Empty<float>();
        private int _bufferOffset;
        private int _bufferCount;
        private WaveFormat? _outputFormat;

        public WasapiCaptureSampleProvider(ProcessLoopbackCapture capture)
        {
            _capture = capture;
            _capture.DataAvailable += OnDataAvailable;
        }

        public WaveFormat WaveFormat => _outputFormat ??= WaveFormat.CreateIeeeFloatWaveFormat(
            _capture.WaveFormat.SampleRate, _capture.WaveFormat.Channels);

        public int Read(float[] buffer, int offset, int count)
        {
            var copied = 0;
            lock (_lock)
            {
                while (copied < count)
                {
                    var available = _bufferCount - _bufferOffset;
                    if (available <= 0) break;

                    var toCopy = Math.Min(available, count - copied);
                    Array.Copy(_buffer, _bufferOffset, buffer, offset + copied, toCopy);
                    _bufferOffset += toCopy;
                    copied += toCopy;
                }
            }
            return copied;
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded == 0) return;

            var format = _capture.WaveFormat;
            var channels = format.Channels;
            if (channels == 0) return;

            var totalSamples = e.BytesRecorded / (format.BitsPerSample / 8);

            lock (_lock)
            {
                if (_buffer.Length < totalSamples)
                    _buffer = new float[totalSamples];

                if (format.Encoding == WaveFormatEncoding.IeeeFloat)
                {
                    Buffer.BlockCopy(e.Buffer, 0, _buffer, 0, e.BytesRecorded);
                }
                else if (format.Encoding == WaveFormatEncoding.Pcm && format.BitsPerSample == 16)
                {
                    var sampleCount = e.BytesRecorded / 2;
                    if (_buffer.Length < sampleCount)
                        _buffer = new float[sampleCount];
                    for (int i = 0; i < sampleCount; i++)
                        _buffer[i] = BitConverter.ToInt16(e.Buffer, i * 2) / 32768f;
                    totalSamples = sampleCount;
                }
                else if (format.Encoding == WaveFormatEncoding.Pcm && format.BitsPerSample == 32)
                {
                    var sampleCount = e.BytesRecorded / 4;
                    if (_buffer.Length < sampleCount)
                        _buffer = new float[sampleCount];
                    for (int i = 0; i < sampleCount; i++)
                        _buffer[i] = BitConverter.ToInt32(e.Buffer, i * 4) / (float)int.MaxValue;
                    totalSamples = sampleCount;
                }
                else
                {
                    return;
                }

                _bufferOffset = 0;
                _bufferCount = totalSamples;
            }
        }
    }
}
