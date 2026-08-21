using System.Collections.Concurrent;
using NAudio.CoreAudioApi;

namespace SoundFXStudio.Services;

/// <summary>
/// Saves and suppresses a game process's audio sessions via the Windows Audio Session API.
///
/// Uses NAudio's AudioSessionManager → SessionCollection → AudioSessionControl → SimpleAudioVolume
/// to enumerate sessions by PID, save their original volume/mute state, suppress them,
/// and restore them later.
///
/// A single process can have multiple audio sessions (music, SFX, voice chat, etc.).
/// ALL sessions matching the target PID must be suppressed for clean loopback audio.
/// </summary>
public sealed class AudioSessionSuppressor : IDisposable
{
    private readonly ConcurrentDictionary<uint, List<SavedSessionState>> _savedStates = new();
    private readonly object _lock = new();
    private MMDeviceEnumerator? _enumerator;
    private bool _disposed;

    /// <summary>
    /// Number of sessions currently suppressed for a given PID.
    /// </summary>
    public int GetSuppressedCount(uint processId)
    {
        return _savedStates.TryGetValue(processId, out var states) ? states.Count : 0;
    }

    /// <summary>
    /// Whether any sessions are currently suppressed.
    /// </summary>
    public bool HasSuppressedSessions => !_savedStates.IsEmpty;

    /// <summary>
    /// Finds all audio sessions for the given process ID on the default render device,
    /// saves their volume/mute state, and suppresses them (volume=0, muted=true).
    /// </summary>
    /// <returns>Number of sessions suppressed.</returns>
    public int SuppressProcess(uint processId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            if (_savedStates.ContainsKey(processId))
                return _savedStates[processId].Count;

            var saved = new List<SavedSessionState>();
            var sessions = GetSessionsForProcess(processId);

            foreach (var session in sessions)
            {
                try
                {
                    var volume = session.SimpleAudioVolume;
                    var state = new SavedSessionState(
                        volume.Volume,
                        volume.Mute);

                    volume.Volume = 0f;
                    volume.Mute = true;
                    saved.Add(state);
                }
                catch
                {
                    // Session died between enumeration and access — skip.
                }
            }

            if (saved.Count > 0)
                _savedStates[processId] = saved;

            return saved.Count;
        }
    }

    /// <summary>
    /// Restores previously suppressed sessions for the given process ID
    /// to their original volume/mute state.
    /// </summary>
    /// <returns>Number of sessions restored.</returns>
    public int RestoreProcess(uint processId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            if (!_savedStates.TryRemove(processId, out var saved))
                return 0;

            return RestoreFromSaved(processId, saved);
        }
    }

    /// <summary>
    /// Restores ALL suppressed sessions and releases resources.
    /// Safe to call multiple times.
    /// </summary>
    public void RestoreAll()
    {
        lock (_lock)
        {
            foreach (var (pid, saved) in _savedStates)
                RestoreFromSaved(pid, saved);

            _savedStates.Clear();
        }
    }

    private int RestoreFromSaved(uint processId, List<SavedSessionState> saved)
    {
        var sessions = GetSessionsForProcess(processId);
        int restored = 0;
        int savedIdx = 0;

        foreach (var session in sessions)
        {
            if (savedIdx >= saved.Count)
                break;

            try
            {
                var volume = session.SimpleAudioVolume;
                var original = saved[savedIdx++];

                volume.Mute = original.Mute;
                volume.Volume = original.Volume;
                restored++;
            }
            catch
            {
                // Session gone — skip.
            }
        }

        return restored;
    }

    private List<AudioSessionControl> GetSessionsForProcess(uint processId)
    {
        var result = new List<AudioSessionControl>();

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var sessionManager = device.AudioSessionManager;
            var sessions = sessionManager.Sessions;

            for (int i = 0; i < sessions.Count; i++)
            {
                try
                {
                    var session = sessions[i];
                    if (session.GetProcessID == processId && !session.IsSystemSoundsSession)
                        result.Add(session);
                }
                catch
                {
                    // Session query failed — skip.
                }
            }
        }
        catch
        {
            // Device enumeration failed — return empty list.
        }

        return result;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        RestoreAll();
        _enumerator?.Dispose();
        _enumerator = null;
    }

    private readonly record struct SavedSessionState(float Volume, bool Mute);
}
