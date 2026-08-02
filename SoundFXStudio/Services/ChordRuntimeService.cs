using SoundFXStudio.Models;

namespace SoundFXStudio.Services;

public sealed class ChordRuntimeService : IDisposable
{
    private readonly Func<AppConfig> _getConfig;
    private readonly Func<string, KeyAssignment?> _resolveAssignmentForKey;
    private readonly Func<KeyAssignment, Task> _executeAssignmentAsync;
    private readonly Func<Guid, Task> _executeActionAsync;
    private readonly object _gate = new();
    private readonly HashSet<string> _held = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _session = new(StringComparer.OrdinalIgnoreCase);

    private bool _overlap;
    private CancellationTokenSource? _timeoutCts;

    public ChordRuntimeService(Func<AppConfig> getConfig, Func<string, KeyAssignment?> resolveAssignmentForKey, Func<KeyAssignment, Task> executeAssignmentAsync, Func<Guid, Task> executeActionAsync)
    {
        _getConfig = getConfig;
        _resolveAssignmentForKey = resolveAssignmentForKey;
        _executeAssignmentAsync = executeAssignmentAsync;
        _executeActionAsync = executeActionAsync;
    }

    public async Task HandleKeyDownAsync(string keyToken)
    {
        var normalizedToken = NormalizeToken(keyToken);
        if (string.IsNullOrWhiteSpace(normalizedToken))
        {
            return;
        }

        Guid? fireActionId = null;

        lock (_gate)
        {
            if (_held.Contains(normalizedToken))
            {
                return;
            }

            _held.Add(normalizedToken);
            _session.Add(normalizedToken);
            if (_held.Count > 1)
            {
                _overlap = true;
            }

            var chords = GetAvailableChords().ToList();

            var satisfied = chords
                .Where(chord => chord.Keys.Select(NormalizeToken).ToHashSet(StringComparer.OrdinalIgnoreCase).IsSubsetOf(_session))
                .OrderByDescending(chord => chord.Keys.Count)
                .FirstOrDefault();

            if (satisfied is not null && !HasLongerPendingChord(chords, satisfied.Keys.Count, _session))
            {
                CancelTimeout();
                _session.Clear();
                _overlap = false;
                fireActionId = satisfied.ActionId;
            }
            else if (CanCompleteChord(chords, _session))
            {
                RestartTimeout(GetChordTimeoutMs());
            }
            else
            {
                RestartTimeout(GetSimultaneousTimeoutMs());
            }
        }

        if (fireActionId is Guid actionId)
        {
            await _executeActionAsync(actionId).ConfigureAwait(false);
        }
    }

    public Task HandleKeyUpAsync(string keyToken)
    {
        var normalizedToken = NormalizeToken(keyToken);
        if (string.IsNullOrWhiteSpace(normalizedToken))
        {
            return Task.CompletedTask;
        }

        lock (_gate)
        {
            _held.Remove(normalizedToken);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            CancelTimeout();
        }

        GC.SuppressFinalize(this);
    }

    private void StartTimeout(int timeoutMs)
    {
        CancelTimeout();
        var cts = new CancellationTokenSource();
        _timeoutCts = cts;
        _ = FirePendingOnTimeoutAsync(timeoutMs, cts.Token);
    }

    private void RestartTimeout(int timeoutMs)
    {
        StartTimeout(timeoutMs);
    }

    private void CancelTimeout()
    {
        _timeoutCts?.Cancel();
        _timeoutCts?.Dispose();
        _timeoutCts = null;
    }

    private async Task FirePendingOnTimeoutAsync(int timeoutMs, CancellationToken token)
    {
        try
        {
            await Task.Delay(timeoutMs, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        List<KeyAssignment>? fireAssignments = null;

        lock (_gate)
        {
            if (token.IsCancellationRequested || _session.Count == 0)
            {
                return;
            }

            var overlap = _overlap;
            var pending = _session.ToList();
            _session.Clear();
            _overlap = false;

            if (overlap)
            {
                // Two or more keys were held together without a matching chord: play nothing.
                return;
            }

            fireAssignments = pending
                .Select(_resolveAssignmentForKey)
                .OfType<KeyAssignment>()
                .ToList();
        }

        if (fireAssignments is { Count: > 0 })
        {
            foreach (var assignment in fireAssignments)
            {
                await _executeAssignmentAsync(assignment).ConfigureAwait(false);
            }
        }
    }

    private int GetChordTimeoutMs()
    {
        try
        {
            var timeout = _getConfig().Settings.ChordTimeoutMs;
            return timeout > 0 ? timeout : 1000;
        }
        catch
        {
            return 1000;
        }
    }

    private int GetSimultaneousTimeoutMs()
    {
        try
        {
            var timeout = _getConfig().Settings.SimultaneousPressTimeoutMs;
            return timeout > 0 ? timeout : 200;
        }
        catch
        {
            return 200;
        }
    }

    private static bool CanCompleteChord(IEnumerable<KeyChord> chords, IReadOnlyCollection<string> session)
    {
        var sessionSet = new HashSet<string>(session, StringComparer.OrdinalIgnoreCase);
        return chords.Any(chord =>
        {
            var keys = chord.Keys.Select(NormalizeToken).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return sessionSet.IsSubsetOf(keys);
        });
    }

    private static bool HasLongerPendingChord(IEnumerable<KeyChord> chords, int satisfiedSize, IReadOnlyCollection<string> session)
    {
        var sessionSet = new HashSet<string>(session, StringComparer.OrdinalIgnoreCase);
        return chords.Any(chord =>
            chord.Keys.Count > satisfiedSize
            && sessionSet.IsSubsetOf(chord.Keys.Select(NormalizeToken).ToHashSet(StringComparer.OrdinalIgnoreCase)));
    }

    private IEnumerable<KeyChord> GetAvailableChords()
    {
        IEnumerable<KeyChord> fromChordSet(IEnumerable<KeyChord> chords)
        {
            return chords ?? Array.Empty<KeyChord>();
        }

        var config = _getConfig();

        foreach (var chord in fromChordSet(config.KeyChords))
        {
            yield return chord;
        }

        foreach (var profile in config.Profiles.Where(profile => string.Equals(profile.Id, config.ActiveProfileId, StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var chord in fromChordSet(profile.KeyChords))
            {
                yield return chord;
            }
        }
    }

    private static string NormalizeToken(string token)
        => token.Trim().ToUpperInvariant();
}
