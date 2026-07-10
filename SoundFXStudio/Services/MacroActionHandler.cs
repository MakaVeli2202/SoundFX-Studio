using SoundFXStudio.Models;

namespace SoundFXStudio.Services;

public sealed class MacroActionHandler : IActionHandler
{
    private readonly AppConfig _config;
    private readonly ConfigService _configService;
    private readonly AudioPlayer _audioPlayer;
    private readonly Func<Guid, CancellationToken, Task> _executeActionAsync;

    public MacroActionHandler(AppConfig config, ConfigService configService, AudioPlayer audioPlayer, Func<Guid, CancellationToken, Task> executeActionAsync)
    {
        _config = config;
        _configService = configService;
        _audioPlayer = audioPlayer;
        _executeActionAsync = executeActionAsync;
    }

    public async Task ExecuteAsync(ActionDefinition action, CancellationToken cancellationToken)
    {
        foreach (var step in ParseSteps(action.Payload))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (step.DelayMs > 0)
            {
                await Task.Delay(step.DelayMs, cancellationToken).ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(step.Command))
            {
                continue;
            }

            switch (step.Command.ToLowerInvariant())
            {
                case "wait":
                    break;
                case "play":
                    await ExecutePlayAsync(step.Argument, cancellationToken).ConfigureAwait(false);
                    break;
                case "stop":
                    StopTarget(step.Argument);
                    break;
                case "switchprofile":
                    SwitchProfile(step.Argument);
                    break;
                case "setvolume":
                    SetVolume(step.Argument);
                    break;
                default:
                    if (Guid.TryParse(step.Command, out var actionId))
                    {
                        await _executeActionAsync(actionId, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        var targetAction = FindAction(step.Command);
                        if (targetAction is not null)
                        {
                            await _executeActionAsync(targetAction.Id, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    break;
            }
        }
    }

    private async Task ExecutePlayAsync(string target, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        if (Guid.TryParse(target, out var actionId))
        {
            await _executeActionAsync(actionId, cancellationToken).ConfigureAwait(false);
            return;
        }

        var targetAction = FindAction(target);
        if (targetAction is not null)
        {
            await _executeActionAsync(targetAction.Id, cancellationToken).ConfigureAwait(false);
            return;
        }

        var sound = _config.Sounds.FirstOrDefault(item => string.Equals(item.Id, target, StringComparison.OrdinalIgnoreCase) || string.Equals(item.Name, target, StringComparison.OrdinalIgnoreCase));
        if (sound is not null)
        {
            var soundAction = _config.Actions.FirstOrDefault(item => item.Type == ActionType.Sound && string.Equals(item.Payload, sound.Id, StringComparison.OrdinalIgnoreCase))
                              ?? CreateSoundAction(sound);

            await _executeActionAsync(soundAction.Id, cancellationToken).ConfigureAwait(false);
        }
    }

    private void StopTarget(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        var sound = _config.Sounds.FirstOrDefault(item => string.Equals(item.Id, target, StringComparison.OrdinalIgnoreCase) || string.Equals(item.Name, target, StringComparison.OrdinalIgnoreCase));
        if (sound is not null)
        {
            _audioPlayer.Stop(sound.Id);
            return;
        }

        var action = FindAction(target);
        if (action is not null && action.Type == ActionType.Sound && !string.IsNullOrWhiteSpace(action.Payload))
        {
            _audioPlayer.Stop(action.Payload);
        }
    }

    private void SwitchProfile(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        var profile = _config.Profiles.FirstOrDefault(item => string.Equals(item.Id, target, StringComparison.OrdinalIgnoreCase)
                                                               || string.Equals(item.Name, target, StringComparison.OrdinalIgnoreCase));
        if (profile is null)
        {
            return;
        }

        _config.ActiveProfileId = profile.Id;
        _configService.Save(_config);
    }

    private void SetVolume(string target)
    {
        if (float.TryParse(target, out var value))
        {
            _config.Settings.MasterVolume = Math.Clamp(value, 0f, 1f);
            _configService.Save(_config);
        }
    }

    private ActionDefinition? FindAction(string token)
        => _config.Actions.FirstOrDefault(item => string.Equals(item.Id.ToString(), token, StringComparison.OrdinalIgnoreCase)
                                                 || string.Equals(item.Name, token, StringComparison.OrdinalIgnoreCase))
           ?? _config.Profiles.SelectMany(profile => profile.Actions).FirstOrDefault(item => string.Equals(item.Id.ToString(), token, StringComparison.OrdinalIgnoreCase)
                                                                                        || string.Equals(item.Name, token, StringComparison.OrdinalIgnoreCase));

    private ActionDefinition CreateSoundAction(SoundEntry sound)
    {
        var action = new ActionDefinition
        {
            Name = sound.Name,
            Description = $"Play {sound.Name}",
            Type = ActionType.Sound,
            IconPath = sound.ImagePath ?? string.Empty,
            Category = sound.Category,
            Payload = sound.Id
        };

        _config.Actions.Add(action);
        return action;
    }

    private static IEnumerable<(string Command, string Argument, int DelayMs)> ParseSteps(string payload)
    {
        foreach (var line in payload.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (TryParseWait(line, out var delayMs))
            {
                yield return ("wait", string.Empty, delayMs);
                continue;
            }

            if (TryParseCommand(line, out var command, out var argument))
            {
                yield return (command, argument, 0);
                continue;
            }

            yield return (line, string.Empty, 0);
        }
    }

    private static bool TryParseCommand(string token, out string command, out string argument)
    {
        var parts = token.Split(new[] { ':' }, 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2)
        {
            command = parts[0];
            argument = parts[1];
            return true;
        }

        parts = token.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 2)
        {
            command = parts[0];
            argument = parts[1];
            return true;
        }

        command = token;
        argument = string.Empty;
        return false;
    }

    private static bool TryParseWait(string token, out int delayMs)
    {
        delayMs = 0;
        if (!token.StartsWith("wait", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var value = token.Contains(':') ? token[(token.IndexOf(':') + 1)..] : token[4..];
        value = value.Trim();
        return int.TryParse(value, out delayMs);
    }
}