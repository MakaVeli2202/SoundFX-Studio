using SoundFXStudio.Models;

namespace SoundFXStudio.Services;

public sealed class ActionExecutor
{
    private readonly AppConfig _config;
    private readonly Dictionary<ActionType, IActionHandler> _handlers;

    public ActionExecutor(AppConfig config, ConfigService configService, AudioPlayer audioPlayer, Func<string, int> resolveOutputDeviceIndex)
    {
        _config = config;

        _handlers = new Dictionary<ActionType, IActionHandler>
        {
            [ActionType.Sound] = new SoundActionHandler(_config, audioPlayer, resolveOutputDeviceIndex),
            [ActionType.Combo] = new ComboActionHandler(_config, audioPlayer, configService, ExecuteAsync),
            [ActionType.Macro] = new MacroActionHandler(_config, configService, audioPlayer, ExecuteAsync),
            [ActionType.Playlist] = new PlaylistActionHandler(_config, ExecuteAsync),
            [ActionType.Profile] = new ProfileActionHandler(_config, configService)
        };
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
        return _config.Profiles
            .SelectMany(profile => profile.Actions)
            .FirstOrDefault(action => action.Id == actionId)
            ?? _config.Actions.FirstOrDefault(action => action.Id == actionId);
    }
}