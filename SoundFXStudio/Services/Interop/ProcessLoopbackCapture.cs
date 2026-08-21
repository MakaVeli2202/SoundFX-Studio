using System.Runtime.InteropServices;
using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;

namespace SoundFXStudio.Services.Interop;

/// <summary>
/// WASAPI per-process loopback capture using the Windows Application Loopback API.
/// Captures audio rendered by a specific process (and its children) via
/// ActivateAudioInterfaceAsync with AUDIOCLIENT_ACTIVATION_TYPE_PROCESS_LOOPBACK.
/// Requires Windows 10 build 20348 or later.
/// </summary>
public sealed class ProcessLoopbackCapture : IDisposable
{
    private const string VirtualAudioDeviceProcessLoopback = @"virtual_\audio_device_process_loopback";
    private const int S_OK = 0;
    private const int VT_BLOB = 65;
    private const uint ProcessLoopbackIncludeTree = 0;
    private const int DefaultBufferMilliseconds = 20;
    private static readonly Guid IAudioClientGuid = new("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");

    private AudioClient? _audioClient;
    private AudioCaptureClient? _captureClient;
    private Thread? _captureThread;
    private CancellationTokenSource? _cts;
    private bool _disposed;
    private WaveFormat _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);

    public event EventHandler<WaveInEventArgs>? DataAvailable;
    public event EventHandler<Exception?>? RecordingStopped;
    public CaptureState CaptureState { get; private set; } = CaptureState.Stopped;
    public WaveFormat WaveFormat => _waveFormat;
    public uint TargetProcessId { get; }

    public ProcessLoopbackCapture(uint targetProcessId)
    {
        TargetProcessId = targetProcessId;
    }

    public void StartRecording()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ProcessLoopbackCapture));
        if (CaptureState == CaptureState.Capturing) return;

        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 20348))
            throw new InvalidOperationException(
                "Application Loopback capture requires Windows 10 build 20348 or later.");

        CaptureState = CaptureState.Capturing;
        _cts = new CancellationTokenSource();

        try
        {
            _audioClient = ActivateProcessLoopbackClient();
            ConfigureAudioClient();
            _captureClient = _audioClient.AudioCaptureClient;

            _captureThread = new Thread(CaptureLoop)
            {
                IsBackground = true,
                Name = "ProcessLoopbackCapture"
            };
            _captureThread.Start(_cts.Token);
        }
        catch
        {
            CaptureState = CaptureState.Stopped;
            CleanupCaptureResources();
            throw;
        }
    }

    public void StopRecording()
    {
        if (CaptureState != CaptureState.Capturing) return;

        CaptureState = CaptureState.Stopping;
        _cts?.Cancel();

        _captureThread?.Join(TimeSpan.FromSeconds(2));

        try { _audioClient?.Stop(); } catch { }

        CleanupCaptureResources();
        CaptureState = CaptureState.Stopped;
        RecordingStopped?.Invoke(this, null);
    }

    private AudioClient ActivateProcessLoopbackClient()
    {
        var activationParams = new AUDIOCLIENT_ACTIVATION_PARAMS
        {
            ActivationType = 1,
            ProcessLoopbackParams = new AUDIOCLIENT_PROCESS_LOOPBACK_PARAMS
            {
                TargetProcessId = TargetProcessId,
                ProcessLoopbackMode = ProcessLoopbackIncludeTree
            }
        };

        var sizeOfParams = Marshal.SizeOf<AUDIOCLIENT_ACTIVATION_PARAMS>();
        var blobDataPtr = Marshal.AllocHGlobal(sizeOfParams);
        try
        {
            Marshal.StructureToPtr(activationParams, blobDataPtr, false);

            var propVariant = new PROPVARIANT
            {
                vt = VT_BLOB,
                blob = new BLOB
                {
                    cbSize = sizeOfParams,
                    pBlobData = blobDataPtr
                }
            };

            var propVariantPtr = Marshal.AllocHGlobal(Marshal.SizeOf<PROPVARIANT>());
            try
            {
                Marshal.StructureToPtr(propVariant, propVariantPtr, false);

                var handler = new ActivateAudioInterfaceCompletionHandler();
                var hr = ActivateAudioInterfaceAsync(
                    VirtualAudioDeviceProcessLoopback,
                    IAudioClientGuid,
                    propVariantPtr,
                    handler,
                    out var asyncOp);

                if (hr != S_OK)
                    throw new InvalidOperationException($"ActivateAudioInterfaceAsync failed: 0x{hr:X8}");

                try
                {
                    if (!handler.Wait(TimeSpan.FromSeconds(10)))
                        throw new InvalidOperationException(
                            "Audio interface activation timed out. Ensure the target process is still running and audio is playing.");

                    asyncOp.GetActivateResult(out var activateResult, out var activateInterface);

                    if (activateResult != S_OK)
                        throw new InvalidOperationException($"Audio interface activation failed: 0x{activateResult:X8}");

                    if (activateInterface is null)
                        throw new InvalidOperationException("Audio interface activation returned null.");

                    if (activateInterface is not IAudioClient iAudioClient)
                        throw new InvalidOperationException(
                            "Activated interface does not support IAudioClient. Ensure Application Loopback is supported on this Windows version.");

                    return new AudioClient(iAudioClient);
                }
                finally
                {
                    if (asyncOp != null)
                        Marshal.ReleaseComObject(asyncOp);
                }
            }
            finally
            {
                Marshal.DestroyStructure<PROPVARIANT>(propVariantPtr);
                Marshal.FreeHGlobal(propVariantPtr);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(blobDataPtr);
        }
    }

    private void ConfigureAudioClient()
    {
        if (_audioClient is null) throw new InvalidOperationException("AudioClient is null.");

        var requestedFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);

        var streamFlags = AudioClientStreamFlags.AutoConvertPcm | AudioClientStreamFlags.SrcDefaultQuality;
        var bufferDuration = (long)(DefaultBufferMilliseconds * 10000f);

        _audioClient.Initialize(
            AudioClientShareMode.Shared,
            streamFlags,
            bufferDuration,
            0,
            requestedFormat,
            Guid.Empty);

        try
        {
            var mixFormat = _audioClient.MixFormat;
            if (mixFormat != null)
                _waveFormat = mixFormat;
        }
        catch
        {
            _waveFormat = requestedFormat;
        }

        _audioClient.Start();
    }

    private void CaptureLoop(object? state)
    {
        var token = (CancellationToken)state!;
        var buffer = new byte[_waveFormat.AverageBytesPerSecond / 10];

        try
        {
            while (!token.IsCancellationRequested)
            {
                if (_captureClient is null || _audioClient is null) break;

                var packetSize = _captureClient.GetNextPacketSize();
                if (packetSize == 0)
                {
                    Thread.Sleep(1);
                    continue;
                }

                var bufferPtr = _captureClient.GetBuffer(
                    out int framesAvailable,
                    out AudioClientBufferFlags bufferFlags,
                    out long devicePosition,
                    out long qpcPosition);

                if (framesAvailable == 0) continue;

                var byteCount = framesAvailable * _waveFormat.BlockAlign;

                if (byteCount > buffer.Length)
                    buffer = new byte[byteCount];

                Marshal.Copy(bufferPtr, buffer, 0, byteCount);
                _captureClient.ReleaseBuffer(framesAvailable);

                var e = new WaveInEventArgs(buffer, byteCount);
                DataAvailable?.Invoke(this, e);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            RecordingStopped?.Invoke(this, ex);
        }
    }

    private void CleanupCaptureResources()
    {
        _cts?.Dispose();
        _cts = null;

        _captureClient = null;

        if (_audioClient != null)
        {
            try { _audioClient.Stop(); } catch { }
            _audioClient.Dispose();
            _audioClient = null;
        }

        _captureThread = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopRecording();
    }

    #region Interop

    [DllImport("mmdevapi.dll", ExactSpelling = true, PreserveSig = true)]
    private static extern int ActivateAudioInterfaceAsync(
        [MarshalAs(UnmanagedType.LPWStr)] string deviceInterfacePath,
        [In] Guid riid,
        [In] IntPtr activationParams,
        [In] IActivateAudioInterfaceCompletionHandler completionHandler,
        out IActivateAudioInterfaceAsyncOperation activationOperation);

    [StructLayout(LayoutKind.Sequential)]
    private struct AUDIOCLIENT_ACTIVATION_PARAMS
    {
        public int ActivationType;
        public AUDIOCLIENT_PROCESS_LOOPBACK_PARAMS ProcessLoopbackParams;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AUDIOCLIENT_PROCESS_LOOPBACK_PARAMS
    {
        public uint TargetProcessId;
        public uint ProcessLoopbackMode;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPVARIANT
    {
        public ushort vt;
        public ushort wReserved1;
        public ushort wReserved2;
        public ushort wReserved3;
        public BLOB blob;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BLOB
    {
        public int cbSize;
        public IntPtr pBlobData;
    }

    #endregion

    #region COM Interfaces

    [ComImport]
    [Guid("726E7786-601C-4C2F-B77C-EDF9D46B8FAB")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IActivateAudioInterfaceCompletionHandler
    {
        void ActivateCompleted(IActivateAudioInterfaceAsyncOperation activateOperation);
    }

    [ComImport]
    [Guid("41D89FB7-DC2F-45EA-B24B-4D69AE951988")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IActivateAudioInterfaceAsyncOperation
    {
        void GetActivateResult(out int activateResult,
            [MarshalAs(UnmanagedType.IUnknown)] out object? activateInterface);
    }

    #endregion

    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    private sealed class ActivateAudioInterfaceCompletionHandler : IActivateAudioInterfaceCompletionHandler
    {
        private readonly ManualResetEventSlim _event = new(false);

        public void ActivateCompleted(IActivateAudioInterfaceAsyncOperation activateOperation)
        {
            _event.Set();
        }

        public bool Wait(TimeSpan timeout) => _event.Wait(timeout);
    }
}
