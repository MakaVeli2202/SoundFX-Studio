using SoundFXStudio.Models;

namespace SoundFXStudio.Services;

public sealed class ChordRuntimeService : IDisposable
{
    private readonly Func<AppConfig> _getConfig;
    private readonly Func<string, KeyAssignment?> _resolveAssignmentForKey;
    private readonly Func<KeyAssignment, Task> _executeAssignmentAsync;
    private readonly Func<Guid, Task> _executeActionAsync;
    private readonly object _gate = new();
    private readonly List<string> _sequence = new();
    private readonly HashSet<string> _heldKeys = new(StringComparer.OrdinalIgnoreCase);

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

        List<KeyAssignment>? fireAssignments = null;
        Guid? fireActionId = null;

        lock (_gate)
        {
            if (_heldKeys.Contains(normalizedToken))
            {
                return;
            }

            _heldKeys.Add(normalizedToken);
            if (!_sequence.Contains(normalizedToken, StringComparer.OrdinalIgnoreCase))
            {
                _sequence.Add(normalizedToken);
            }

            var chords = GetAvailableChords().ToList();

            var satisfied = chords
                .Where(chord => chord.Keys.Select(NormalizeToken).ToHashSet(StringComparer.OrdinalIgnoreCase).IsSubsetOf(_sequence))
                .OrderByDescending(chord => chord.Keys.Count)
                .FirstOrDefault();

            if (satisfied is not null && !HasLongerPendingChord(chords, satisfied.Keys.Count, _sequence))
            {
                CancelTimeout();
                _sequence.Clear();
                fireActionId = satisfied.ActionId;
            }
            else if (CanCompleteChord(chords, _sequence))
            {
                if (_sequence.Count == 1)
                {
                    StartTimeout();
                }
                else
                {
                    RestartTimeout();
                }
            }
            else
            {
                CancelTimeout();
                var pending = _sequence.ToList();
                _sequence.Clear();
                fireAssignments = pending
                    .Select(_resolveAssignmentForKey)
                    .OfType<KeyAssignment>()
                    .ToList();
            }
        }

        if (fireAssignments is { Count: > 0 })
        {
            foreach (var assignment in fireAssignments)
            {
                await _executeAssignmentAsync(assignment).ConfigureAwait(false);
            }
        }
        else if (fireActionId is Guid actionId)
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
            _heldKeys.Remove(normalizedToken);
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

    private void StartTimeout()
    {
        CancelTimeout();
        var cts = new CancellationTokenSource();
        _timeoutCts = cts;
        _ = FirePendingOnTimeoutAsync(cts.Token);
    }

    private void RestartTimeout()
    {
        StartTimeout();
    }

    private void CancelTimeout()
    {
        _timeoutCts?.Cancel();
        _timeoutCts?.Dispose();
        _timeoutCts = null;
    }

    private async Task FirePendingOnTimeoutAsync(CancellationToken token)
    {
        var timeoutMs = GetChordTimeoutMs();

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
            if (token.IsCancellationRequested || _sequence.Count == 0)
            {
                return;
            }

            var pending = _sequence.ToList();
            _sequence.Clear();
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

    private static bool CanCompleteChord(IEnumerable<KeyChord> chords, IReadOnlyCollection<string> sequence)
    {
        var sequenceSet = new HashSet<string>(sequence, StringComparer.OrdinalIgnoreCase);
        return chords.Any(chord =>
        {
            var keys = chord.Keys.Select(NormalizeToken).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return sequenceSet.IsSubsetOf(keys);
        });
    }

    private static bool HasLongerPendingChord(IEnumerable<KeyChord> chords, int satisfiedSize, IReadOnlyCollection<string> sequence)
    {
        var sequenceSet = new HashSet<string>(sequence, StringComparer.OrdinalIgnoreCase);
        return chords.Any(chord =>
            chord.Keys.Count > satisfiedSize
            && sequenceSet.IsSubsetOf(chord.Keys.Select(NormalizeToken).ToHashSet(StringComparer.OrdinalIgnoreCase)));
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
