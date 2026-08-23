using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace SoundFXStudio.Services.DSP;

/// <summary>
/// Combined pitch + character shifter for the voice changer.
/// One phase-vocoder pass folds pitch and formant factors into a single
/// effective factor, so cascading STFT artifacts stay low and the voice
/// stays natural. Formant factor acts as an accent on top of pitch:
/// &gt;1 brightens (female/child), &lt;1 darkens (male/deep), since a plain
/// SMB pitch shift already drags formants with it.
/// </summary>
public sealed class VoiceTransformSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private ISampleProvider? _stage;
    private float _pitchFactor = 1f;
    private float _formantFactor = 1f;

    public VoiceTransformSampleProvider(ISampleProvider source)
    {
        _source = source;
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public float PitchFactor
    {
        get => _pitchFactor;
        set
        {
            value = Math.Clamp(value, 0.25f, 4f);
            if (Math.Abs(value - _pitchFactor) < 0.001f)
            {
                return;
            }
            _pitchFactor = value;
            Rebuild();
        }
    }

    public float FormantFactor
    {
        get => _formantFactor;
        set
        {
            value = Math.Clamp(value, 0.5f, 2f);
            if (Math.Abs(value - _formantFactor) < 0.001f)
            {
                return;
            }
            _formantFactor = value;
            Rebuild();
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        return _stage is null ? _source.Read(buffer, offset, count) : _stage.Read(buffer, offset, count);
    }

    private void Rebuild()
    {
        var effective = _pitchFactor * _formantFactor;
        if (Math.Abs(effective - 1f) < 0.001f)
        {
            _stage = null;
            return;
        }

        // FFT 2048 keeps voice frequency resolution with half the time smearing
        // of 4096 (~42ms vs ~85ms), preserving consonant clarity.
        // osamp 16 gives smooth overlap-add reconstruction with minimal artifacts.
        _stage = new SmbPitchShiftingSampleProvider(_source, 2048, 16, effective);
    }
}
