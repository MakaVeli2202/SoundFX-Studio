using Concentus.Enums;
using NAudio.Wave;
using System;
using System.Threading;

namespace SoundFXStudio.Services;

/// <summary>
/// Monitors what the team hears: captures the VoiceMeeter B1 output mix
/// (the exact signal your mic sends to Discord) and plays it back through
/// your physical speakers/headphones. Optionally emulates Discord's Opus
/// re-encode so you hear what teammates actually receive.
/// </summary>
public sealed class TeamMonitorService : IDisposable
{
    public enum SimMode
    {
        None,
        Opus64Mono,
        Opus128Stereo
    }

    private WaveInEvent? _waveIn;
    private WaveOutEvent? _waveOut;
    private BufferedWaveProvider? _rawBuffer;
    private BufferedWaveProvider? _simBuffer;
    private CancellationTokenSource? _cts;
    private Thread? _simThread;
    private SimMode _mode;
    private bool _running;
    private bool _disposed;

    public bool IsRunning => _running;
    public string? LastError { get; private set; }

    public bool Start(int captureDeviceIndex, int playbackDeviceIndex, SimMode mode)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(TeamMonitorService));
        Stop();
        _mode = mode;
        LastError = null;

        try
        {
            int sampleRate = 48000;
            var captureFormat = new WaveFormat(sampleRate, 16, 2);
            var outputFormat = mode == SimMode.Opus64Mono
                ? new WaveFormat(sampleRate, 16, 1)
                : captureFormat;

            _waveIn = new WaveInEvent
            {
                DeviceNumber = captureDeviceIndex,
                WaveFormat = captureFormat,
                BufferMilliseconds = 100
            };
            _waveIn.DataAvailable += OnDataAvailable;

            _rawBuffer = new BufferedWaveProvider(captureFormat)
            {
                DiscardOnBufferOverflow = true
            };

            if (mode == SimMode.None)
            {
                _waveOut = new WaveOutEvent { DeviceNumber = playbackDeviceIndex };
                _waveOut.Init(_rawBuffer);
            }
            else
            {
                _simBuffer = new BufferedWaveProvider(outputFormat)
                {
                    DiscardOnBufferOverflow = true
                };
                _waveOut = new WaveOutEvent { DeviceNumber = playbackDeviceIndex };
                _waveOut.Init(_simBuffer);

                _cts = new CancellationTokenSource();
                _simThread = new Thread(() => SimLoop(_cts.Token, sampleRate, mode))
                {
                    IsBackground = true,
                    Name = "TeamMonitor-Opus"
                };
                _simThread.Start();
            }

            _waveIn.StartRecording();
            _waveOut.Play();
            _running = true;
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            CleanupAudio();
            return false;
        }
    }

    public void Stop()
    {
        _running = false;
        _cts?.Cancel();

        if (_waveIn is not null)
        {
            _waveIn.DataAvailable -= OnDataAvailable;
            try { _waveIn.StopRecording(); } catch { }
            _waveIn.Dispose();
            _waveIn = null;
        }

        if (_waveOut is not null)
        {
            try { _waveOut.Stop(); } catch { }
            _waveOut.Dispose();
            _waveOut = null;
        }

        _rawBuffer = null;
        _simBuffer = null;

        try { _simThread?.Join(500); } catch { }
        _simThread = null;
        _cts?.Dispose();
        _cts = null;
    }

    public void SetVolume(float volume)
    {
        if (_waveOut is not null)
        {
            try { _waveOut.Volume = Math.Clamp(volume, 0f, 1f); } catch { }
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        _rawBuffer?.AddSamples(e.Buffer, 0, e.BytesRecorded);
    }

    private void SimLoop(CancellationToken token, int sampleRate, SimMode mode)
    {
        try
        {
            int channels = mode == SimMode.Opus64Mono ? 1 : 2;
            int frameSamples = sampleRate / 50; // 20 ms frame, Discord-style
            int frameBytes = frameSamples * 2 * 2;
            int frameBytesOut = frameSamples * 2 * channels;

            var encoder = Concentus.OpusCodecFactory.CreateEncoder(sampleRate, channels, OpusApplication.OPUS_APPLICATION_VOIP);
            encoder.Bitrate = mode == SimMode.Opus64Mono ? 64000 : 128000;

            var decoder = Concentus.OpusCodecFactory.CreateDecoder(sampleRate, channels);

            byte[] pcmIn = new byte[frameBytes];
            short[] encodeIn = new short[frameSamples * 2];
            short[] encodeOut = new short[frameSamples * 2];
            byte[] encoded = new byte[4096];
            byte[] pcmOut = new byte[frameBytesOut];

            while (!token.IsCancellationRequested)
            {
                if (_rawBuffer is null)
                    break;

                int got = ReadFull(_rawBuffer, pcmIn, frameBytes, token);
                if (got < frameBytes)
                {
                    Thread.Sleep(5);
                    continue;
                }

                for (int i = 0; i < frameSamples; i++)
                {
                    int left = pcmIn[i * 4] | (pcmIn[i * 4 + 1] << 8);
                    int right = pcmIn[i * 4 + 2] | (pcmIn[i * 4 + 3] << 8);
                    if (channels == 1)
                    {
                        encodeIn[i] = (short)((left + right) / 2);
                    }
                    else
                    {
                        encodeIn[i * 2] = (short)left;
                        encodeIn[i * 2 + 1] = (short)right;
                    }
                }

                int encodedLength = encoder.Encode(encodeIn, frameSamples, encoded, encoded.Length);
                int decoded = decoder.Decode(encoded.AsSpan(0, encodedLength), encodeOut, frameSamples, false);
                if (decoded <= 0)
                    continue;

                int samplesOut = decoded * channels;
                for (int i = 0; i < samplesOut; i++)
                {
                    short v = encodeOut[i];
                    pcmOut[i * 2] = (byte)(v & 0xFF);
                    pcmOut[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
                }

                _simBuffer?.AddSamples(pcmOut, 0, samplesOut * 2);
            }
        }
        catch (OperationCanceledException) { }
        catch
        {
            // Simulator should never crash the app; monitor just goes silent.
        }
    }

    private static int ReadFull(BufferedWaveProvider buffer, byte[] dst, int count, CancellationToken token)
    {
        int offset = 0;
        while (offset < count)
        {
            if (token.IsCancellationRequested)
                break;
            int n = buffer.Read(dst, offset, count - offset);
            if (n <= 0)
                break;
            offset += n;
        }
        return offset;
    }

    public static int ResolveWaveInIndex(string deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return -1;

        try
        {
            var trimmed = deviceName.Trim();
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var productName = WaveIn.GetCapabilities(i).ProductName;
                var truncated = trimmed.Length > 31 ? trimmed[..31] : trimmed;
                if (productName.StartsWith(truncated, StringComparison.OrdinalIgnoreCase) ||
                    truncated.StartsWith(productName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }
        catch
        {
            // fall back to default device
        }

        return -1;
    }

    public static int ResolveWaveOutIndex(string deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return -1;

        try
        {
            var trimmed = deviceName.Trim();
            for (int i = 0; i < WaveOut.DeviceCount; i++)
            {
                var productName = WaveOut.GetCapabilities(i).ProductName;
                var truncated = trimmed.Length > 31 ? trimmed[..31] : trimmed;
                if (productName.StartsWith(truncated, StringComparison.OrdinalIgnoreCase) ||
                    truncated.StartsWith(productName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }
        catch
        {
            // fall back to default device
        }

        return -1;
    }

    private void CleanupAudio()
    {
        if (_waveIn is not null)
        {
            _waveIn.DataAvailable -= OnDataAvailable;
            try { _waveIn.StopRecording(); } catch { }
            _waveIn.Dispose();
            _waveIn = null;
        }
        if (_waveOut is not null)
        {
            try { _waveOut.Stop(); } catch { }
            _waveOut.Dispose();
            _waveOut = null;
        }
        _rawBuffer = null;
        _simBuffer = null;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _simThread = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
