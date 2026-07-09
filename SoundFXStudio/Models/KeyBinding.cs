namespace SoundFXStudio.Models;

public class KeyBinding
{
    public List<string> Keys { get; set; } = new();

    public bool IsSequence { get; set; }

    public int TimeoutMs { get; set; } = 1000;
}
