namespace SoundFXStudio.Services.DSP;

public sealed class ReverbEffect : IAudioEffect
{
    private static readonly double[] CombDelaysSec = { 0.0297, 0.0371, 0.0411, 0.0437 };
    private static readonly double[] AllpassDelaysSec = { 0.0050, 0.0017 };

    private float[]?[] _combBuffers;
    private float[]?[] _allpassBuffers;
    private int[] _combIndex;
    private int[] _allpassIndex;
    private double[] _lowpass;

    public ReverbEffect(int sampleRate = 48000)
    {
        SampleRate = sampleRate;
        _combBuffers = new float[]?[CombDelaysSec.Length];
        _allpassBuffers = new float[]?[AllpassDelaysSec.Length];
        _combIndex = new int[CombDelaysSec.Length];
        _allpassIndex = new int[AllpassDelaysSec.Length];
        _lowpass = new double[CombDelaysSec.Length];
        Allocate();
    }

    public string Name => "Reverb";

    public bool IsEnabled { get; set; }

    public int SampleRate { get; set; }

    public double Mix { get; set; } = 0.35;

    public double RoomSize { get; set; } = 0.7;

    public double Damp { get; set; } = 0.3;

    public void Process(Span<float> buffer)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            var input = (double)buffer[i];

            var combSum = 0.0;
            for (int c = 0; c < _combBuffers.Length; c++)
            {
                var delay = _combBuffers[c]!;
                var idx = _combIndex[c];
                var delayed = delay[idx];

                _lowpass[c] += Damp * (delayed - _lowpass[c]);
                delay[idx] = (float)(input + RoomSize * _lowpass[c]);
                idx = (idx + 1) % delay.Length;
                _combIndex[c] = idx;

                combSum += delayed;
            }

            var signal = combSum / _combBuffers.Length;
            for (int a = 0; a < _allpassBuffers.Length; a++)
            {
                var delay = _allpassBuffers[a]!;
                var idx = _allpassIndex[a];
                var delayed = delay[idx];

                delay[idx] = (float)(signal + 0.5 * delayed);
                idx = (idx + 1) % delay.Length;
                _allpassIndex[a] = idx;

                signal = delayed - 0.5 * signal;
            }

            buffer[i] = (float)((1.0 - Mix) * input + Mix * signal);
        }
    }

    public void Reset()
    {
        Array.Clear(_lowpass, 0, _lowpass.Length);
        for (int i = 0; i < _combBuffers.Length; i++)
        {
            Array.Clear(_combBuffers[i]!, 0, _combBuffers[i]!.Length);
        }
        for (int i = 0; i < _allpassBuffers.Length; i++)
        {
            Array.Clear(_allpassBuffers[i]!, 0, _allpassBuffers[i]!.Length);
        }
    }

    private void Allocate()
    {
        for (int i = 0; i < _combBuffers.Length; i++)
        {
            _combBuffers[i] = new float[(int)Math.Max(1, CombDelaysSec[i] * SampleRate)];
        }
        for (int i = 0; i < _allpassBuffers.Length; i++)
        {
            _allpassBuffers[i] = new float[(int)Math.Max(1, AllpassDelaysSec[i] * SampleRate)];
        }
    }
}
