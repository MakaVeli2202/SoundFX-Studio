namespace SoundFXStudio.Models;

public class ComboStep
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public ComboStepType Type { get; set; }

    public Guid TargetId { get; set; }

    public int DelayMs { get; set; }

    public float Volume { get; set; } = 1f;
}
