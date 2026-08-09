using SoundFXStudio.Models;
using System.IO;

namespace SoundFXStudio.Services;

public sealed class SoundActionHandler : IActionHandler
{
    private readonly Func<AppConfig> _getConfig;
    private readonly AudioPlayer _audioPlayer;
    private readonly Func<string, int> _resolveOutputDeviceIndex;
    private readonly AudioDeviceService _audioDeviceService = new();

    public SoundActionHandler(Func<AppConfig> getConfig, AudioPlayer audioPlayer, Func<string, int> resolveOutputDeviceIndex)
    {
        _getConfig = getConfig;
        _audioPlayer = audioPlayer;
        _resolveOutputDeviceIndex = resolveOutputDeviceIndex;
    }

    public Task ExecuteAsync(ActionDefinition action, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(action.Payload))
        {
            return Task.CompletedTask;
        }

        var config = _getConfig();
        var sound = config.Sounds.FirstOrDefault(item => string.Equals(item.Id, action.Payload, StringComparison.OrdinalIgnoreCase));
        var filePath = sound?.FilePath;
        if (sound is null || string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return Task.CompletedTask;
        }

        // Keep keyboard-triggered sounds on the same Voicemeeter path as voice-changer output
        // so teammates hear them on B1 without changing Windows input devices.
        var outputDeviceIndex = _audioDeviceService.ResolveVoicemeeterInputWaveOutIndex();
        if (outputDeviceIndex < 0)
        {
            outputDeviceIndex = _resolveOutputDeviceIndex(config.Settings.OutputDeviceId);
        }

        App.EnsureVirtualInputB1();
        _audioPlayer.Play(sound.Id, filePath, sound.Volume, action.PlaybackMode, outputDeviceIndex);
        return Task.CompletedTask;
    }
}