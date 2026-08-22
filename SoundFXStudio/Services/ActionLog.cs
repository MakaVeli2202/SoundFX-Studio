using System.Collections.Concurrent;
using System.IO;

namespace SoundFXStudio.Services;

/// <summary>
/// Captures every user-facing action, status message, and error to a timestamped log file.
/// Enables post-hoc debugging without needing to watch the UI in real time.
/// Thread-safe. Writes are buffered and flushed periodically.
/// </summary>
public sealed class ActionLog : IDisposable
{
    private static readonly Lazy<ActionLog> _instance = new(() => new ActionLog());
    public static ActionLog Instance => _instance.Value;

    private readonly string _logDir;
    private readonly ConcurrentQueue<(DateTime time, string level, string source, string message)> _entries = new();
    private readonly Timer _flushTimer;
    private readonly object _writeLock = new();
    private bool _disposed;

    private ActionLog()
    {
        _logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        Directory.CreateDirectory(_logDir);
        _flushTimer = new Timer(_ => Flush(), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    }

    public void Info(string source, string message) => Enqueue("INFO", source, message);
    public void Warn(string source, string message) => Enqueue("WARN", source, message);
    public void Error(string source, string message) => Enqueue("ERROR", source, message);
    public void Error(string source, string message, Exception ex) => Enqueue("ERROR", source, $"{message}: {ex.GetType().Name}: {ex.Message}");

    /// <summary>
    /// Log a button/action click with its result.
    /// </summary>
    public void Action(string source, string action, string result = "OK")
        => Enqueue("ACTION", source, $"{action} → {result}");

    private void Enqueue(string level, string source, string message)
    {
        _entries.Enqueue((DateTime.Now, level, source, message));
    }

    public void Flush()
    {
        if (_entries.IsEmpty) return;

        lock (_writeLock)
        {
            try
            {
                var fileName = $"actionlog-{DateTime.Now:yyyy-MM-dd}.log";
                var filePath = Path.Combine(_logDir, fileName);

                using var writer = new StreamWriter(filePath, append: true);
                while (_entries.TryDequeue(out var entry))
                {
                    writer.WriteLine($"{entry.time:HH:mm:ss.fff} [{entry.level,-7}] {entry.source}: {entry.message}");
                }
            }
            catch
            {
                // Never let logging crash the app
            }
        }
    }

    /// <summary>
    /// Returns the path to today's log file for easy reading.
    /// </summary>
    public string TodayLogPath => Path.Combine(_logDir, $"actionlog-{DateTime.Now:yyyy-MM-dd}.log");

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _flushTimer.Dispose();
        Flush();
    }
}
