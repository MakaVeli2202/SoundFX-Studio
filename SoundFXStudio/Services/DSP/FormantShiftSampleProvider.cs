using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace SoundFXStudio.Services.DSP;

public sealed class FormantShiftSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly int _sampleRate;
    private ISampleProvider? _stage;
    private float _factor = 1f;

    public FormantShiftSampleProvider(ISampleProvider source)
    {
        _source = source;
        _sampleRate = source.WaveFormat.SampleRate;
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public float Factor
    {
        get => _factor;
        set
        {
            value = Math.Clamp(value, 0.5f, 2f);
            if (Math.Abs(value - _factor) < 0.001f)
            {
                return;
            }
            _factor = value;
            Rebuild();
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        return _stage is null ? _source.Read(buffer, offset, count) : _stage.Read(buffer, offset, count);
    }

    private void Rebuild()
    {
        if (Math.Abs(_factor - 1f) < 0.001f)
        {
            _stage = null;
            return;
        }

        var resampledRate = (int)Math.Round(_sampleRate * _factor);
        var resampled = new WdlResamplingSampleProvider(_source, resampledRate);
        var pitchCorrected = new SmbPitchShiftingSampleProvider(resampled)
        {
            PitchFactor = 1f / _factor
        };
        _stage = new WdlResamplingSampleProvider(pitchCorrected, _sampleRate);
    }
}
