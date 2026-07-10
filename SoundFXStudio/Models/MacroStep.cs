namespace SoundFXStudio.Models;

public class MacroStep
{
    public int DelayMs { get; set; }

    public string Command { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public float Volume { get; set; } = 1f;
}
