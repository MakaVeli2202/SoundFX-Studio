using SoundFXStudio.Models;
using System.IO;

namespace SoundFXStudio.Services;

public sealed class SoundActionHandler : IActionHandler
{
    private readonly AppConfig _config;
    private readonly AudioPlayer _audioPlayer;
    private readonly Func<string, int> _resolveOutputDeviceIndex;

    public SoundActionHandler(AppConfig config, AudioPlayer audioPlayer, Func<string, int> resolveOutputDeviceIndex)
    {
        _config = config;
        _audioPlayer = audioPlayer;
        _resolveOutputDeviceIndex = resolveOutputDeviceIndex;
    }

    public Task ExecuteAsync(ActionDefinition action, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(action.Payload))
        {
            return Task.CompletedTask;
        }

        var sound = _config.Sounds.FirstOrDefault(item => string.Equals(item.Id, action.Payload, StringComparison.OrdinalIgnoreCase));
        var filePath = sound?.FilePath;
        if (sound is null || string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return Task.CompletedTask;
        }

        var outputDeviceIndex = _resolveOutputDeviceIndex(_config.Settings.OutputDeviceId);
        _audioPlayer.Play(sound.Id, filePath, sound.Volume, sound.Loop, action.PlaybackMode, outputDeviceIndex);
        return Task.CompletedTask;
    }
}