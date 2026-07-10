namespace SoundFXStudio.Models;

public class AudioRoutingProfile
{
    public bool Speakers { get; set; }

    public bool Headphones { get; set; }

    public bool Microphone { get; set; }

    public bool VirtualCable { get; set; }

    public bool OBS { get; set; }
}
