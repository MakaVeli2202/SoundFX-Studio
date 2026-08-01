using SoundFXStudio.Models;
using System.Globalization;

namespace SoundFXStudio.Services;

public sealed class CommandActionHandler : IActionHandler
{
    private readonly Func<AppConfig> _getConfig;
    private readonly AudioPlayer _audioPlayer;
    private readonly Func<string, int> _resolveOutputDeviceIndex;

    public CommandActionHandler(Func<AppConfig> getConfig, AudioPlayer audioPlayer, Func<string, int> resolveOutputDeviceIndex)
    {
        _getConfig = getConfig;
        _audioPlayer = audioPlayer;
        _resolveOutputDeviceIndex = resolveOutputDeviceIndex;
    }

    public Task ExecuteAsync(ActionDefinition action, CancellationToken cancellationToken)
    {
        switch (action.Type)
        {
            case ActionType.StopAllPlayback:
                _audioPlayer.StopAll();
                break;

            case ActionType.StopPlayback:
                if (!string.IsNullOrWhiteSpace(action.Payload))
                {
                    _audioPlayer.Stop(action.Payload);
                }
                break;

            case ActionType.VolumeChange:
                if (float.TryParse(action.Payload?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var target))
                {
                    _audioPlayer.SetMasterVolume(target <= 1f ? target : target / 100f);
                }
                break;

            case ActionType.DeviceSwitch:
                if (!string.IsNullOrWhiteSpace(action.Payload))
                {
                    _resolveOutputDeviceIndex(action.Payload);
                    _getConfig().Settings.OutputDeviceId = action.Payload;
                }
                break;
        }

        return Task.CompletedTask;
    }
}
