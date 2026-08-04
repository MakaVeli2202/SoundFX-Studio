using SoundFXStudio.Infrastructure;

namespace SoundFXStudio.Models;

public enum HotkeyAction
{
    MuteAll,
    MuteHear,
    MuteTeam,
    StopAll,
    SoundboardToggle,
    VoiceChangerToggle
}

public sealed class KeybindingItem : ObservableObject
{
    private string _key = string.Empty;

    public KeybindingItem(HotkeyAction action, string key)
    {
        Action = action;
        Key = key;
    }

    public HotkeyAction Action { get; }

    public string ActionName => GetActionName(Action);

    public string Key
    {
        get => _key;
        set => SetProperty(ref _key, value);
    }

    public static string GetActionName(HotkeyAction action) => action switch
    {
        HotkeyAction.MuteAll => "Mute All",
        HotkeyAction.MuteHear => "Mute Hear",
        HotkeyAction.MuteTeam => "Mute Team",
        HotkeyAction.StopAll => "Stop All Sounds",
        HotkeyAction.SoundboardToggle => "Toggle Soundboard",
        HotkeyAction.VoiceChangerToggle => "Toggle Voice Changer",
        _ => action.ToString()
    };

    public static HotkeyAction? ParseAction(string name) => name switch
    {
        "Mute All" => HotkeyAction.MuteAll,
        "Mute Hear" => HotkeyAction.MuteHear,
        "Mute Team" => HotkeyAction.MuteTeam,
        "Stop All Sounds" => HotkeyAction.StopAll,
        "Toggle Soundboard" => HotkeyAction.SoundboardToggle,
        "Toggle Voice Changer" => HotkeyAction.VoiceChangerToggle,
        _ => null
    };
}
