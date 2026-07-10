using SoundFXStudio.Models;

namespace SoundFXStudio.Services;

public sealed class PlaylistActionHandler : IActionHandler
{
    private readonly AppConfig _config;
    private readonly Func<Guid, CancellationToken, Task> _executeActionAsync;

    public PlaylistActionHandler(AppConfig config, Func<Guid, CancellationToken, Task> executeActionAsync)
    {
        _config = config;
        _executeActionAsync = executeActionAsync;
    }

    public async Task ExecuteAsync(ActionDefinition action, CancellationToken cancellationToken)
    {
        var actionIds = ParseActionIds(action.Payload).ToList();
        if (actionIds.Count == 0)
        {
            return;
        }

        if (string.Equals(action.PlaylistMode, "Single", StringComparison.OrdinalIgnoreCase))
        {
            await _executeActionAsync(actionIds[0], cancellationToken).ConfigureAwait(false);
            return;
        }

        var isShuffle = string.Equals(action.PlaylistMode, "Shuffle", StringComparison.OrdinalIgnoreCase);
        var isRepeat = string.Equals(action.PlaylistMode, "Repeat", StringComparison.OrdinalIgnoreCase);

        do
        {
            var orderedIds = isShuffle ? actionIds.OrderBy(_ => Random.Shared.Next()).ToList() : actionIds;

            foreach (var actionId in orderedIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (_config.Actions.Any(item => item.Id == actionId) || _config.Profiles.SelectMany(profile => profile.Actions).Any(item => item.Id == actionId))
                {
                    await _executeActionAsync(actionId, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        while (isRepeat && !cancellationToken.IsCancellationRequested);
    }

    private static IEnumerable<Guid> ParseActionIds(string payload)
    {
        foreach (var token in payload.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Guid.TryParse(token, out var actionId))
            {
                yield return actionId;
            }
        }
    }
}