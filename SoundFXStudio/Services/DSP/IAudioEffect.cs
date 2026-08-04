namespace SoundFXStudio.Services.DSP;

public interface IAudioEffect
{
    string Name { get; }

    bool IsEnabled { get; set; }

    void Process(Span<float> buffer);

    void Reset();
}
