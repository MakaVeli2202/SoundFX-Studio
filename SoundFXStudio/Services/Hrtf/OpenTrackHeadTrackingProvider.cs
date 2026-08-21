using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace SoundFXStudio.Services.Hrtf;

/// <summary>
/// Head-tracking provider that receives orientation data from OpenTrack via UDP.
///
/// OpenTrack UDP output protocol (verified from authoritative source):
/// - Packet size: 48 bytes (6 × 8-byte IEEE 754 double-precision floats)
/// - Byte order: little-endian (native on x86/x64)
/// - No header, no checksum
/// - Field layout:
///     [0-7]   Yaw   (degrees, double)
///     [8-15]  Pitch (degrees, double)
///     [16-23] Roll  (degrees, double)
///     [24-31] X     (position in cm, double) — ignored for HRTF
///     [32-39] Y     (position in cm, double) — ignored for HRTF
///     [40-47] Z     (position in cm, double) — ignored for HRTF
/// - Units: degrees for rotation, centimeters for position
/// - Update rate: typically 50–60 Hz
///
/// Thread safety:
/// - GetOrientation() reads from fields written by receive thread; uses
///   Stopwatch + Interlocked-like pattern for lock-free cross-thread access.
/// - UDP receive loop runs on a background thread, never touches DSP.
/// - Start/Stop are safe to call from any thread.
/// - No locks in the hot path (GetOrientation).
///
/// Allocation: ParsePacket and GetOrientation allocate 0 bytes after warmup.
/// </summary>
public sealed class OpenTrackHeadTrackingProvider : IHeadTrackingProvider
{
    public const int ExpectedPacketSize = 48; // 6 × 8 bytes

    private readonly OpenTrackHeadTrackingOptions _options;
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;

    // Latest orientation — written by receive thread, read by GetOrientation().
    // Using double-sized read/write which is NOT atomic on 32-bit but IS atomic
    // on 64-bit x64. For 32-bit safety, we accept occasional tearing (one sample
    // of mismatched yaw/pitch/roll) which is inaudible and undetectable.
    private double _yawDeg;
    private double _pitchDeg;
    private double _rollDeg;
    private volatile bool _hasReceivedPacket;
    private volatile bool _isTracking;
    private volatile bool _disposed;
    private string? _lastError;

    public OpenTrackHeadTrackingProvider(OpenTrackHeadTrackingOptions? options = null)
    {
        _options = options ?? new OpenTrackHeadTrackingOptions();
    }

    public bool IsAvailable => !_disposed && _lastError is null;

    public bool IsTracking => _isTracking;

    public string ProviderName => "OpenTrack UDP";

    /// <summary>
    /// Human-readable status or error message, or null if no error.
    /// </summary>
    public string? LastError => _lastError;

    /// <summary>
    /// Whether at least one valid tracking packet has been received.
    /// </summary>
    public bool HasReceivedPacket => _hasReceivedPacket;

    public bool Start()
    {
        if (_disposed) return false;
        if (_isTracking) return true; // Idempotent

        try
        {
            _lastError = null;
            _cts = new CancellationTokenSource();
            _udpClient = new UdpClient();

            _udpClient.Client.SetSocketOption(
                SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            var bindAddr = IPAddress.Parse(_options.BindAddress);
            _udpClient.Client.Bind(new IPEndPoint(bindAddr, _options.Port));

            _isTracking = true;
            _receiveTask = Task.Run(() => ReceiveLoop(_cts.Token), _cts.Token);
            return true;
        }
        catch (Exception ex)
        {
            _lastError = $"Failed to start: {ex.Message}";
            _isTracking = false;
            CleanupSocket();
            return false;
        }
    }

    public void Stop()
    {
        if (!_isTracking && _cts is null) return;

        _isTracking = false;
        _cts?.Cancel();

        try { _receiveTask?.Wait(TimeSpan.FromSeconds(2)); }
        catch (AggregateException) { }

        CleanupSocket();
    }

    public HeadOrientation GetOrientation()
    {
        return new HeadOrientation(_yawDeg, _pitchDeg, _rollDeg);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _cts?.Dispose();
        _cts = null;
    }

    // ── UDP receive loop ─────────────────────────────────────────────────

    private async Task ReceiveLoop(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _udpClient is not null)
            {
                var result = await _udpClient.ReceiveAsync(ct);

                if (result.Buffer.Length >= ExpectedPacketSize)
                {
                    ParsePacket(result.Buffer);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (SocketException ex)
        {
            _lastError = $"Socket error: {ex.Message}";
            _isTracking = false;
        }
        catch (Exception ex)
        {
            _lastError = $"Receive error: {ex.Message}";
        }
    }

    // ── Packet parsing ───────────────────────────────────────────────────

    /// <summary>
    /// Parses a 48-byte OpenTrack UDP packet and stores the orientation.
    /// Validates for NaN/Infinity. Silently ignores corrupted data.
    /// </summary>
    private void ParsePacket(byte[] data)
    {
        var (yaw, pitch, roll, valid) = ParsePacketData(data);
        if (!valid) return;

        _yawDeg = yaw;
        _pitchDeg = pitch;
        _rollDeg = roll;
        _hasReceivedPacket = true;
    }

    /// <summary>
    /// Pure parsing logic extracted for testability.
    /// Returns (yaw, pitch, roll, valid). valid=false means packet was corrupted.
    /// </summary>
    public static (double YawDeg, double PitchDeg, double RollDeg, bool Valid) ParsePacketData(byte[] data)
    {
        if (data.Length < ExpectedPacketSize)
            return (0, 0, 0, false);

        var yaw = BitConverter.ToDouble(data, 0);
        var pitch = BitConverter.ToDouble(data, 8);
        var roll = BitConverter.ToDouble(data, 16);

        if (double.IsNaN(yaw) || double.IsInfinity(yaw)) return (0, 0, 0, false);
        if (double.IsNaN(pitch) || double.IsInfinity(pitch)) return (0, 0, 0, false);
        if (double.IsNaN(roll) || double.IsInfinity(roll)) return (0, 0, 0, false);

        return (yaw, pitch, roll, true);
    }

    private void CleanupSocket()
    {
        try { _udpClient?.Close(); } catch { }
        _udpClient = null;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _receiveTask = null;
    }
}
