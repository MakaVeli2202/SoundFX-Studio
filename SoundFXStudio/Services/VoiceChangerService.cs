using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SoundFXStudio.Services.DSP;

namespace SoundFXStudio.Services;

public sealed class VoiceChangerService : IDisposable
{
    private WasapiCapture? _capture;
    private WaveInEvent? _waveIn;
    private WaveOutEvent? _waveOut;
    private BufferedWaveProvider? _buffer;
    private SmbPitchShiftingSampleProvider? _pitchShifter;
    private FormantShiftSampleProvider? _formantShifter;
    private EffectSampleProvider? _effectProvider;
    private float _pitchSemitones;
    private float _formantShift = 1f;
    private bool _disposed;

    public bool IsRunning { get; private set; }

    public float PitchSemitones => _pitchSemitones;

    public float FormantShift => _formantShift;

    public DSPChain Chain { get; }

    public VoiceChangerService()
    {
        Chain = new DSPChain();
        Chain.Add(new NoiseGateEffect(44100));
        Chain.Add(new LimiterEffect(44100));
        Chain.Add(new CompressorEffect(44100));
        Chain.Add(new DistortionEffect(44100));
        Chain.Add(new ReverbEffect(44100));
        Chain.Add(new RobotEffect(44100));
        Chain.Add(new ChorusEffect(44100));
    }

    public void Start(string? micDeviceId, int outputDeviceIndex, float pitchSemitones)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(VoiceChangerService));
        if (IsRunning) Stop();

        _pitchSemitones = pitchSemitones;

        ISampleProvider sampleProvider;
        try
        {
            var captureDevice = new AudioDeviceService().GetCaptureDevice(micDeviceId);
            if (captureDevice is null) throw new InvalidOperationException("No WASAPI capture device available.");

            _capture = new WasapiCapture(captureDevice, useEventSync: true);
            _buffer = new BufferedWaveProvider(_capture.WaveFormat)
            {
                DiscardOnBufferOverflow = true
            };
            _capture.DataAvailable += OnWasapiDataAvailable;
            _capture.StartRecording();

            sampleProvider = _buffer.ToSampleProvider();
            if (_capture.WaveFormat.Channels > 1)
            {
                sampleProvider = new DownmixToMonoSampleProvider(sampleProvider);
            }
        }
        catch
        {
            if (_capture is not null)
            {
                _capture.DataAvailable -= OnWasapiDataAvailable;
                _capture.Dispose();
                _capture = null;
            }
            _buffer = null;

            _waveIn = new WaveInEvent
            {
                DeviceNumber = 0,
                WaveFormat = new WaveFormat(44100, 1)
            };
            _buffer = new BufferedWaveProvider(_waveIn.WaveFormat)
            {
                DiscardOnBufferOverflow = true
            };
            _waveIn.DataAvailable += OnWaveInDataAvailable;
            _waveIn.StartRecording();
            sampleProvider = _buffer.ToSampleProvider();
        }

        _pitchShifter = new SmbPitchShiftingSampleProvider(sampleProvider)
        {
            PitchFactor = SemitonesToFactor(_pitchSemitones)
        };

        _formantShifter = new FormantShiftSampleProvider(_pitchShifter)
        {
            Factor = _formantShift
        };

        _effectProvider = new EffectSampleProvider(_formantShifter, Chain);

        _waveOut = new WaveOutEvent
        {
            DeviceNumber = outputDeviceIndex
        };

        _waveOut.Init(_effectProvider);

        _waveOut.Play();

        IsRunning = true;
    }

    public void Stop()
    {
        if (!IsRunning) return;

        IsRunning = false;

        if (_capture is not null)
        {
            _capture.DataAvailable -= OnWasapiDataAvailable;
            try { _capture.StopRecording(); } catch { }
            _capture.Dispose();
            _capture = null;
        }

        if (_waveIn is not null)
        {
            _waveIn.DataAvailable -= OnWaveInDataAvailable;
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
        _formantShifter = null;
        _effectProvider = null;
    }

    public void SetPitch(float semitones)
    {
        _pitchSemitones = semitones;
        if (_pitchShifter is not null)
        {
            _pitchShifter.PitchFactor = SemitonesToFactor(semitones);
        }
    }

    public void SetFormant(float factor)
    {
        _formantShift = factor;
        if (_formantShifter is not null)
        {
            _formantShifter.Factor = factor;
        }
    }

    private void OnWasapiDataAvailable(object? sender, WasapiCaptureEventArgs e)
    {
        _buffer?.AddSamples(e.Buffer, 0, e.BytesRecorded);
    }

    private void OnWaveInDataAvailable(object? sender, WaveInEventArgs e)
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
