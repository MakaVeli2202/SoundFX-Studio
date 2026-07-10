using SoundFXStudio.Models;

namespace SoundFXStudio.Services;

public sealed class ChordRuntimeService
{
    private readonly AppConfig _config;
    private readonly Func<string, KeyAssignment?> _resolveAssignmentForKey;
    private readonly Func<KeyAssignment, Task> _executeAssignmentAsync;
    private readonly Func<Guid, Task> _executeActionAsync;
    private readonly HashSet<string> _pressedKeys = new(StringComparer.OrdinalIgnoreCase);

    private string? _pendingSingleKeyToken;
    private KeyChord? _pendingChordCandidate;
    private bool _chordFiredThisCycle;

    public ChordRuntimeService(AppConfig config, Func<string, KeyAssignment?> resolveAssignmentForKey, Func<KeyAssignment, Task> executeAssignmentAsync, Func<Guid, Task> executeActionAsync)
    {
        _config = config;
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

        _pressedKeys.Add(normalizedToken);

        if (_chordFiredThisCycle)
        {
            return;
        }

        var match = FindBestMatch(_pressedKeys);
        if (match is not null && match.Keys.Count > 1)
        {
            if (HasLongerMatch(_pressedKeys, match.Keys.Count))
            {
                _pendingChordCandidate = match;
                return;
            }

            _pendingSingleKeyToken = null;
            _pendingChordCandidate = null;
            _chordFiredThisCycle = true;
            await _executeActionAsync(match.ActionId).ConfigureAwait(false);
            return;
        }

        if (_pressedKeys.Count == 1)
        {
            _pendingSingleKeyToken = normalizedToken;
        }
    }

    public async Task HandleKeyUpAsync(string keyToken)
    {
        var normalizedToken = NormalizeToken(keyToken);
        if (string.IsNullOrWhiteSpace(normalizedToken))
        {
            return;
        }

        _pressedKeys.Remove(normalizedToken);

        if (_pressedKeys.Count > 0)
        {
            return;
        }

        try
        {
            if (!_chordFiredThisCycle && _pendingChordCandidate is not null)
            {
                _chordFiredThisCycle = true;
                await _executeActionAsync(_pendingChordCandidate.ActionId).ConfigureAwait(false);
                return;
            }

            if (!_chordFiredThisCycle && _pendingSingleKeyToken is not null)
            {
                var assignment = _resolveAssignmentForKey(_pendingSingleKeyToken);
                if (assignment is not null)
                {
                    await _executeAssignmentAsync(assignment).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _pendingSingleKeyToken = null;
            _pendingChordCandidate = null;
            _chordFiredThisCycle = false;
            _pressedKeys.Clear();
        }
    }

    private KeyChord? FindBestMatch(IEnumerable<string> pressedKeys)
    {
        var pressedSet = new HashSet<string>(pressedKeys, StringComparer.OrdinalIgnoreCase);
        if (pressedSet.Count == 0)
        {
            return null;
        }

        return GetAvailableChords()
            .Where(chord => chord.Keys.Count > 1)
            .Where(chord => chord.Keys.Count == pressedSet.Count)
            .Where(chord => chord.Keys.Select(NormalizeToken).ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(pressedSet))
            .OrderByDescending(chord => chord.Keys.Count)
            .FirstOrDefault();
    }

    private bool HasLongerMatch(IEnumerable<string> pressedKeys, int currentSize)
    {
        var pressedSet = new HashSet<string>(pressedKeys, StringComparer.OrdinalIgnoreCase);

        return GetAvailableChords().Any(chord => chord.Keys.Count > currentSize
                                                && chord.Keys.Select(NormalizeToken).ToHashSet(StringComparer.OrdinalIgnoreCase).IsSupersetOf(pressedSet));
    }

    private IEnumerable<KeyChord> GetAvailableChords()
    {
        IEnumerable<KeyChord> fromChordSet(IEnumerable<KeyChord> chords)
        {
            return chords ?? Array.Empty<KeyChord>();
        }

        foreach (var chord in fromChordSet(_config.KeyChords))
        {
            yield return chord;
        }

        foreach (var profile in _config.Profiles.Where(profile => string.Equals(profile.Id, _config.ActiveProfileId, StringComparison.OrdinalIgnoreCase)))
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