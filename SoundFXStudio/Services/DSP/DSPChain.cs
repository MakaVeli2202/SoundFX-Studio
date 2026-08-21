namespace SoundFXStudio.Services.DSP;

public sealed class DSPChain : IAudioEffect
{
    private readonly List<IAudioEffect> _effects = new();

    public string Name => "DSP Chain";

    public bool IsEnabled { get; set; } = true;

    public IReadOnlyList<IAudioEffect> Effects => _effects;

    public void Add(IAudioEffect effect) => _effects.Add(effect);

    public void Remove(IAudioEffect effect) => _effects.Remove(effect);

    public void Clear() => _effects.Clear();

    public T? Get<T>() where T : class, IAudioEffect =>
        _effects.OfType<T>().FirstOrDefault();

    public void Process(Span<float> buffer)
    {
        if (!IsEnabled)
        {
            return;
        }

        for (int i = 0; i < _effects.Count; i++)
        {
            var effect = _effects[i];
            if (effect.IsEnabled)
            {
                try
                {
                    effect.Process(buffer);
                }
                catch
                {
                }
            }
        }
    }

    public void Reset()
    {
        for (int i = 0; i < _effects.Count; i++)
        {
            _effects[i].Reset();
        }
    }
}
