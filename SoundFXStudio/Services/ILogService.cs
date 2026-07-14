namespace SoundFXStudio.Services;

public interface ILogService : IDisposable
{
    bool Enabled { get; set; }

    void Trace(string message);

    void Info(string message);

    void Warning(string message);

    void Error(string message, Exception? exception = null);

    void Critical(string message, Exception? exception = null);
}
