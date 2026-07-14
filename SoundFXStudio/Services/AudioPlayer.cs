using SoundFXStudio.Models;
using NAudio.Wave;
using System.IO;

namespace SoundFXStudio.Services;

public sealed class AudioPlayer : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, List<PlaybackSession>> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogService? _logService;

    public AudioPlayer(ILogService? logService = null)
    {
        _logService = logService;
    }

    public void Play(string soundId, string filePath, float volume = 1f, bool loop = false, PlaybackMode playbackMode = PlaybackMode.Restart, int outputDeviceNumber = -1)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                _logService?.Warning($"Missing Audio File: {Path.GetFileName(filePath)}");
                return;
            }

            var existingSessions = GetSessions(soundId);

            if (playbackMode == PlaybackMode.Ignore && existingSessions.Count > 0)
            {
                return;
            }

            if (playbackMode == PlaybackMode.Toggle)
            {
                if (existingSessions.Count > 0)
                {
                    Stop(soundId);
                    _logService?.Info($"Playback Stopped: {soundId}");
                    return;
                }
            }

            if (playbackMode == PlaybackMode.Restart)
            {
                Stop(soundId);
                _logService?.Info($"Playback Stopped: {soundId}");
            }

            var reader = new AudioFileReader(filePath)
            {
                Volume = Math.Clamp(volume, 0f, 1f)
            };

            IWaveProvider provider = loop ? new LoopStream(reader) : reader;
            var output = new WaveOutEvent
            {
                DeviceNumber = outputDeviceNumber
            };

            output.Init(provider);

            var session = new PlaybackSession(reader, output);

            output.PlaybackStopped += (_, _) => RemoveSession(soundId, session);

            lock (_gate)
            {
                if (!_sessions.TryGetValue(soundId, out var sessions))
                {
                    sessions = new List<PlaybackSession>();
                    _sessions[soundId] = sessions;
                }

                sessions.Add(session);
            }

            output.Play();
            _logService?.Info($"Playback Started: {soundId}");
        }
        catch (Exception ex)
        {
            _logService?.Error($"Playback Failed: {soundId}", ex);
        }
    }

    public void Stop(string soundId)
    {
        List<PlaybackSession> sessions = GetSessions(soundId);

        foreach (var session in sessions)
        {
            session.Stop();
            RemoveSession(soundId, session);
        }

        if (sessions.Count > 0)
        {
            _logService?.Info($"Playback Stopped: {soundId}");
        }
    }

    public async Task FadeOutAndStopAsync(string soundId, int milliseconds)
    {
        var sessions = GetSessions(soundId);
        if (sessions.Count == 0)
        {
            return;
        }

        var steps = Math.Max(1, milliseconds / 20);
        _logService?.Info($"FadeOut Started: {soundId}");

        foreach (var session in sessions)
        {
            var current = session.Reader.Volume;

            for (var i = steps; i >= 0; i--)
            {
                session.Reader.Volume = current * i / steps;
                await Task.Delay(20).ConfigureAwait(false);
            }

            session.Stop();
            RemoveSession(soundId, session);
        }

        _logService?.Info($"FadeOut Completed: {soundId}");
    }

    public void StopAll()
    {
        List<PlaybackSession> sessions;
        lock (_gate)
        {
            sessions = _sessions.Values.SelectMany(item => item).ToList();
            _sessions.Clear();
        }

        foreach (var session in sessions)
        {
            session.Stop();
        }

        if (sessions.Count > 0)
        {
            _logService?.Info("Playback Stopped");
        }
    }

    public bool IsPlaying(string soundId)
    {
        lock (_gate)
        {
            return _sessions.TryGetValue(soundId, out var sessions) && sessions.Count > 0;
        }
    }

    public void Dispose() => StopAll();

    private void RemoveSession(string soundId, PlaybackSession? session)
    {
        if (session is null)
        {
            return;
        }

        lock (_gate)
        {
            if (_sessions.TryGetValue(soundId, out var sessions))
            {
                sessions.RemoveAll(existing => ReferenceEquals(existing, session));

                if (sessions.Count == 0)
                {
                    _sessions.Remove(soundId);
                }
            }
        }

        session.Dispose();
    }

    private List<PlaybackSession> GetSessions(string soundId)
    {
        lock (_gate)
        {
            if (_sessions.TryGetValue(soundId, out var sessions))
            {
                return sessions.ToList();
            }

            return new List<PlaybackSession>();
        }
    }

    private sealed class PlaybackSession : IDisposable
    {
        public PlaybackSession(AudioFileReader reader, WaveOutEvent output)
        {
            Reader = reader;
            Output = output;
        }

        public AudioFileReader Reader { get; }

        public WaveOutEvent Output { get; }

        public void Stop()
        {
            try
            {
                Output.Stop();
            }
            catch
            {
                // ignore stop races
            }
        }

        public void Dispose()
        {
            Output.Dispose();
            Reader.Dispose();
        }
    }

    private sealed class LoopStream : WaveStream
    {
        private readonly WaveStream _sourceStream;

        public LoopStream(WaveStream sourceStream)
        {
            _sourceStream = sourceStream;
        }

        public override WaveFormat WaveFormat => _sourceStream.WaveFormat;

        public override long Length => _sourceStream.Length;

        public override long Position
        {
            get => _sourceStream.Position;
            set => _sourceStream.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int totalBytesRead = 0;

            while (totalBytesRead < count)
            {
                int bytesRead = _sourceStream.Read(buffer, offset + totalBytesRead, count - totalBytesRead);
                if (bytesRead == 0)
                {
                    _sourceStream.Position = 0;
                    bytesRead = _sourceStream.Read(buffer, offset + totalBytesRead, count - totalBytesRead);
                    if (bytesRead == 0)
                    {
                        break;
                    }
                }

                totalBytesRead += bytesRead;
            }

            return totalBytesRead;
        }
    }
}