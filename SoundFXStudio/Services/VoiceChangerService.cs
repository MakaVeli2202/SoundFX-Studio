using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace SoundFXStudio.Services;

public sealed class VoiceChangerService : IDisposable
{
    private WaveInEvent? _waveIn;
    private WaveOutEvent? _waveOut;
    private BufferedWaveProvider? _buffer;
    private SmbPitchShiftingSampleProvider? _pitchShifter;
    private bool _disposed;

    public bool IsRunning { get; private set; }

    public void Start(int micDeviceIndex, int outputDeviceIndex, float pitchSemitones)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(VoiceChangerService));
        if (IsRunning) Stop();

        _waveIn = new WaveInEvent
        {
            DeviceNumber = micDeviceIndex,
            WaveFormat = new WaveFormat(44100, 1)
        };

        _buffer = new BufferedWaveProvider(_waveIn.WaveFormat)
        {
            DiscardOnBufferOverflow = true
        };

        var sampleProvider = _buffer.ToSampleProvider();

        _pitchShifter = new SmbPitchShiftingSampleProvider(sampleProvider)
        {
            PitchFactor = SemitonesToFactor(pitchSemitones)
        };

        _waveOut = new WaveOutEvent
        {
            DeviceNumber = outputDeviceIndex
        };

        _waveOut.Init(_pitchShifter);

        _waveIn.DataAvailable += OnDataAvailable;

        _waveIn.StartRecording();
        _waveOut.Play();

        IsRunning = true;
    }

    public void Stop()
    {
        if (!IsRunning) return;

        IsRunning = false;

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

        _buffer = null;
        _pitchShifter = null;
    }

    public void SetPitch(float semitones)
    {
        _pitchShifter!.PitchFactor = SemitonesToFactor(semitones);
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        _buffer?.AddSamples(e.Buffer, 0, e.BytesRecorded);
    }

    private static float SemitonesToFactor(float semitones)
    {
        return MathF.Pow(2f, semitones / 12f);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
