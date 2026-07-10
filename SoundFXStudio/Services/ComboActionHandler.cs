using SoundFXStudio.Models;

namespace SoundFXStudio.Services;

public sealed class ComboActionHandler : IActionHandler
{
    private readonly AppConfig _config;
    private readonly AudioPlayer _audioPlayer;
    private readonly ConfigService _configService;
    private readonly Func<Guid, CancellationToken, Task> _executeActionAsync;

    public ComboActionHandler(AppConfig config, AudioPlayer audioPlayer, ConfigService configService, Func<Guid, CancellationToken, Task> executeActionAsync)
    {
        _config = config;
        _audioPlayer = audioPlayer;
        _configService = configService;
        _executeActionAsync = executeActionAsync;
    }

    public async Task ExecuteAsync(ActionDefinition action, CancellationToken cancellationToken)
    {
        var combo = ResolveCombo(action.Payload);
        if (combo is null || !combo.IsEnabled)
        {
            return;
        }

        foreach (var step in combo.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (step.DelayMs > 0)
            {
                await Task.Delay(step.DelayMs, cancellationToken).ConfigureAwait(false);
            }

            switch (step.Type)
            {
                case ComboStepType.Wait:
                    break;
                case ComboStepType.PlaySound:
                    await PlaySoundAsync(step.TargetId, step.Volume, cancellationToken).ConfigureAwait(false);
                    break;
                case ComboStepType.StopSound:
                    StopSound(step.TargetId);
                    break;
                case ComboStepType.StopAll:
                    _audioPlayer.StopAll();
                    break;
                case ComboStepType.ExecuteAction:
                    await _executeActionAsync(step.TargetId, cancellationToken).ConfigureAwait(false);
                    break;
                case ComboStepType.StartPlaylist:
                    await ExecutePlaylistAsync(step.TargetId, cancellationToken).ConfigureAwait(false);
                    break;
                case ComboStepType.SwitchProfile:
                    SwitchProfile(step.TargetId);
                    break;
            }
        }
    }

    private ComboDefinition? ResolveCombo(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        return _config.Combos.FirstOrDefault(item => string.Equals(item.Id.ToString(), payload, StringComparison.OrdinalIgnoreCase)
                                                     || string.Equals(item.Name, payload, StringComparison.OrdinalIgnoreCase))
               ?? _config.Profiles.SelectMany(profile => profile.Combos).FirstOrDefault(item => string.Equals(item.Id.ToString(), payload, StringComparison.OrdinalIgnoreCase)
                                                                                           || string.Equals(item.Name, payload, StringComparison.OrdinalIgnoreCase));
    }

    private async Task PlaySoundAsync(Guid targetId, float volume, CancellationToken cancellationToken)
    {
        if (targetId == Guid.Empty)
        {
            return;
        }

        var sound = _config.Sounds.FirstOrDefault(item => string.Equals(item.Id, targetId.ToString(), StringComparison.OrdinalIgnoreCase));
        if (sound is null)
        {
            return;
        }

        var soundAction = _config.Actions.FirstOrDefault(item => item.Type == ActionType.Sound && string.Equals(item.Payload, sound.Id, StringComparison.OrdinalIgnoreCase))
                          ?? CreateSoundAction(sound);

        soundAction.PlaybackMode = PlaybackMode.Restart;

        if (Math.Abs(volume - 1f) > float.Epsilon)
        {
            sound.Volume = volume;
        }

        await _executeActionAsync(soundAction.Id, cancellationToken).ConfigureAwait(false);
    }

    private void StopSound(Guid targetId)
    {
        if (targetId == Guid.Empty)
        {
            return;
        }

        _audioPlayer.Stop(targetId.ToString());
    }

    private async Task ExecutePlaylistAsync(Guid targetId, CancellationToken cancellationToken)
    {
        if (targetId == Guid.Empty)
        {
            return;
        }

        var playlistAction = _config.Actions.FirstOrDefault(item => item.Id == targetId && item.Type == ActionType.Playlist)
                            ?? _config.Profiles.SelectMany(profile => profile.Actions).FirstOrDefault(item => item.Id == targetId && item.Type == ActionType.Playlist);

        if (playlistAction is not null)
        {
            await _executeActionAsync(playlistAction.Id, cancellationToken).ConfigureAwait(false);
        }
    }

    private void SwitchProfile(Guid targetId)
    {
        if (targetId == Guid.Empty)
        {
            return;
        }

        var profile = _config.Profiles.FirstOrDefault(item => item.Id == targetId.ToString());
        if (profile is null)
        {
            return;
        }

        _config.ActiveProfileId = profile.Id;
        _configService.Save(_config);
    }

    private ActionDefinition CreateSoundAction(SoundEntry sound)
    {
        var action = new ActionDefinition
        {
            Name = sound.Name,
            Description = $"Play {sound.Name}",
            Type = ActionType.Sound,
            IconPath = sound.ImagePath ?? string.Empty,
            Category = sound.Category,
            Payload = sound.Id,
            PlaybackMode = PlaybackMode.Restart
        };

        _config.Actions.Add(action);
        return action;
    }
}