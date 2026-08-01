using SoundFXStudio.Models;

namespace SoundFXStudio.Services;

public sealed class ActionExecutor
{
    private readonly Func<AppConfig> _getConfig;
    private readonly Dictionary<ActionType, IActionHandler> _handlers;

    public ActionExecutor(Func<AppConfig> getConfig, ConfigService configService, AudioPlayer audioPlayer, Func<string, int> resolveOutputDeviceIndex)
    {
        _getConfig = getConfig;

        _handlers = new Dictionary<ActionType, IActionHandler>
        {
            [ActionType.Sound] = new SoundActionHandler(_getConfig, audioPlayer, resolveOutputDeviceIndex),
            [ActionType.Combo] = new ComboActionHandler(_getConfig, audioPlayer, configService, ExecuteAsync),
            [ActionType.Macro] = new MacroActionHandler(_getConfig, configService, audioPlayer, ExecuteAsync),
            [ActionType.Playlist] = new PlaylistActionHandler(_getConfig, ExecuteAsync),
            [ActionType.Profile] = new ProfileActionHandler(_getConfig, configService)
        };

        var commandHandler = new CommandActionHandler(_getConfig, audioPlayer, resolveOutputDeviceIndex);
        _handlers[ActionType.StopPlayback] = commandHandler;
        _handlers[ActionType.StopAllPlayback] = commandHandler;
        _handlers[ActionType.VolumeChange] = commandHandler;
        _handlers[ActionType.DeviceSwitch] = commandHandler;
    }

    public Task ExecuteAsync(Guid actionId)
        => ExecuteAsync(actionId, CancellationToken.None);

    public async Task ExecuteAsync(Guid actionId, CancellationToken cancellationToken)
    {
        var action = ResolveAction(actionId);
        if (action is null || !action.IsEnabled)
        {
            return;
        }

        if (_handlers.TryGetValue(action.Type, out var handler))
        {
            await handler.ExecuteAsync(action, cancellationToken).ConfigureAwait(false);
        }
    }

    private ActionDefinition? ResolveAction(Guid actionId)
    {
        var config = _getConfig();
        return config.Profiles
            .SelectMany(profile => profile.Actions)
            .FirstOrDefault(action => action.Id == actionId)
            ?? config.Actions.FirstOrDefault(action => action.Id == actionId);
    }
}