using System.Threading.Channels;
using System.IO;

namespace SoundFXStudio.Services;

public sealed class FileLogService : ILogService
{
    private readonly Channel<LogEntry> _channel;
    private readonly Task _worker;
    private readonly string _logFolder;
    private readonly object _disposeGate = new();
    private bool _disposed;

    public FileLogService()
    {
        _logFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SoundFXStudio", "Logs");
        _channel = Channel.CreateUnbounded<LogEntry>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

        _worker = Task.Run(ProcessQueueAsync);
    }

    public bool Enabled { get; set; } = true;

    public void Trace(string message) => Write(LogLevel.Trace, message);

    public void Info(string message) => Write(LogLevel.Info, message);

    public void Warning(string message) => Write(LogLevel.Warning, message);

    public void Error(string message, Exception? exception = null) => Write(LogLevel.Error, message, exception);

    public void Critical(string message, Exception? exception = null) => Write(LogLevel.Critical, message, exception);

    public void Dispose()
    {
        lock (_disposeGate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        try
        {
            _channel.Writer.TryComplete();
            _worker.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // ignore logger shutdown issues
        }
    }

    private void Write(LogLevel level, string message, Exception? exception = null)
    {
        if (!Enabled)
        {
            return;
        }

        try
        {
            _channel.Writer.TryWrite(new LogEntry(DateTime.Now, level, message, exception));
        }
        catch
        {
            // logging must never break app flow
        }
    }

    private async Task ProcessQueueAsync()
    {
        try
        {
            await foreach (var entry in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                TryWriteEntry(entry);
            }
        }
        catch
        {
            // ignore logger worker issues
        }
    }

    private void TryWriteEntry(LogEntry entry)
    {
        try
        {
            Directory.CreateDirectory(_logFolder);
            var path = Path.Combine(_logFolder, $"{entry.Timestamp:yyyy-MM-dd}.log");
            File.AppendAllText(path, Format(entry));
        }
        catch
        {
            // ignore logger write failures
        }
    }

    private static string Format(LogEntry entry)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append(entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        builder.Append(" [");
        builder.Append(entry.Level.ToString().ToUpperInvariant());
        builder.Append("] ");
        builder.AppendLine(entry.Message);

        if (entry.Exception is not null)
        {
            builder.AppendLine($"Exception Type: {entry.Exception.GetType().FullName}");
            builder.AppendLine($"Message: {entry.Exception.Message}");
            builder.AppendLine("Stack Trace:");
            builder.AppendLine(entry.Exception.StackTrace ?? string.Empty);
        }

        return builder.ToString();
    }

    private sealed record LogEntry(DateTime Timestamp, LogLevel Level, string Message, Exception? Exception);
}
