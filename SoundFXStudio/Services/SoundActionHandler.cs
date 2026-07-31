using SoundFXStudio.Models;
using System.IO;

namespace SoundFXStudio.Services;

public sealed class SoundActionHandler : IActionHandler
{
    private readonly Func<AppConfig> _getConfig;
    private readonly AudioPlayer _audioPlayer;
    private readonly Func<string, int> _resolveOutputDeviceIndex;

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

        var outputDeviceIndex = _resolveOutputDeviceIndex(config.Settings.OutputDeviceId);
        _audioPlayer.Play(sound.Id, filePath, sound.Volume, sound.Loop, action.PlaybackMode, outputDeviceIndex);
        return Task.CompletedTask;
    }
}