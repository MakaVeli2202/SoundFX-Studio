using System.Net;

namespace SoundFXStudio.Services.Hrtf;

/// <summary>
/// Configuration for the OpenTrack UDP head-tracking provider.
/// </summary>
public sealed class OpenTrackHeadTrackingOptions
{
    private int _port = 4242;
    private string _bindAddress = "127.0.0.1";

    /// <summary>
    /// UDP port to listen on. Default: 4242 (OpenTrack default).
    /// </summary>
    public int Port
    {
        get => _port;
        set
        {
            if (value < 1 || value > 65535)
                throw new ArgumentOutOfRangeException(nameof(value), "Port must be 1–65535.");
            _port = value;
        }
    }

    /// <summary>
    /// IP address to bind. Default: "127.0.0.1" (localhost only).
    /// </summary>
    public string BindAddress
    {
        get => _bindAddress;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("BindAddress cannot be empty.", nameof(value));
            if (!IPAddress.TryParse(value, out _))
                throw new ArgumentException($"'{value}' is not a valid IP address.", nameof(value));
            _bindAddress = value;
        }
    }

    /// <summary>
    /// If true, the provider starts automatically on creation.
    /// </summary>
    public bool AutoStart { get; set; }
}
