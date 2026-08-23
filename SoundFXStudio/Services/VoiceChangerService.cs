using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SoundFXStudio.Models;
using SoundFXStudio.Services.DSP;

namespace SoundFXStudio.Services;

public sealed class VoiceChangerService : IDisposable
{
    private WasapiCapture? _capture;
    private WaveInEvent? _waveIn;
    private WaveOutEvent? _waveOut;
    private BufferedWaveProvider? _buffer;
    private VoiceTransformSampleProvider? _transform;
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
        Chain.Add(new EqualizerEffect(44100) { IsEnabled = true });
        Chain.Add(new CompressorEffect(44100));
        Chain.Add(new LimiterEffect(44100));
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

        int rate = _capture?.WaveFormat.SampleRate
                   ?? _waveIn?.WaveFormat.SampleRate
                   ?? 0;
        if (rate > 0)
        {
            SetChainSampleRate(rate);
        }

        _transform = new VoiceTransformSampleProvider(sampleProvider)
        {
            PitchFactor = SemitonesToFactor(_pitchSemitones),
            FormantFactor = _formantShift
        };

        _effectProvider = new EffectSampleProvider(_transform, Chain);

        IsRunning = true;
        try
        {
            _waveOut = new WaveOutEvent
            {
                DeviceNumber = outputDeviceIndex
            };

            _waveOut.Init(_effectProvider);

            _waveOut.Play();
        }
        catch
        {
            Stop();
            throw;
        }
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
        _transform = null;
        _effectProvider = null;
    }

    public void SetPitch(float semitones)
    {
        _pitchSemitones = semitones;
        if (_transform is not null)
        {
            _transform.PitchFactor = SemitonesToFactor(semitones);
        }
    }

    public void SetFormant(float factor)
    {
        _formantShift = factor;
        if (_transform is not null)
        {
            _transform.FormantFactor = factor;
        }
    }

    private void SetChainSampleRate(int sampleRate)
    {
        foreach (var effect in Chain.Effects)
        {
            switch (effect)
            {
                case NoiseGateEffect noiseGate:
                    noiseGate.SampleRate = sampleRate;
                    break;
                case EqualizerEffect eq:
                    eq.SampleRate = sampleRate;
                    break;
                case CompressorEffect compressor:
                    compressor.SampleRate = sampleRate;
                    break;
                case LimiterEffect limiter:
                    limiter.SampleRate = sampleRate;
                    break;
                case DistortionEffect distortion:
                    distortion.SampleRate = sampleRate;
                    break;
                case ReverbEffect reverb:
                    reverb.SampleRate = sampleRate;
                    break;
                case RobotEffect robot:
                    robot.SampleRate = sampleRate;
                    break;
                case ChorusEffect chorus:
                    chorus.SampleRate = sampleRate;
                    break;
            }
        }
    }

    private void OnWasapiDataAvailable(object? sender, WaveInEventArgs e)
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
