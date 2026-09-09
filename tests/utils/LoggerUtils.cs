namespace SyncDirs.Tests;

internal sealed class LoggerUtils : ISyncLogger
{
    public List<string> Info { get; } = new();
    public List<string> Warn { get; } = new();
    public List<string> Error { get; } = new();

    void ISyncLogger.Info(string message) => Info.Add(message);
    void ISyncLogger.Warn(string message) => Warn.Add(message);
    void ISyncLogger.Error(string message) => Error.Add(message);
}