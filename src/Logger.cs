using System.Globalization;

namespace SyncDirs;

internal interface ISyncLogger 
{
    void Info(string message);
    void Warn(string message);
    void Error(string message);
}

internal sealed class Logger : ISyncLogger, IDisposable 
{
    private readonly Lock _loggerLock = new();
    private readonly StreamWriter _logWriter;
    private bool _exceptionLaunched = false;

    public Logger(string logFilePath)
    {
        // if log path given is inside a nonexistent directory - create directory first and then create log file

        string? directory = Path.GetDirectoryName(logFilePath);
        if(!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        _logWriter = new StreamWriter(new FileStream(logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
        {
            AutoFlush = true
        };
    }

    public void Info(string message) => Write("[INFO ]", message, Console.Out);
    public void Warn(string message) => Write("[WARN ]", message, Console.Error);
    public void Error(string message) => Write("[ERROR]", message, Console.Error);

    private void Write(string level, string message, TextWriter console)
    {
        DateTimeOffset dateTime = DateTimeOffset.Now;

        string output = $"{dateTime.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture)} {level} {message}";
        
        lock(_loggerLock)
        {
            console.WriteLine(output);                

            try
            {
                _logWriter.WriteLine(output);
            }
            catch(Exception e)
            {
                if(!_exceptionLaunched)
                {
                    Console.Error.WriteLine($"Failed trying to log to file: {e.Message}");
                    _exceptionLaunched = true;
                }
            }
        }
    }

    public void Dispose()
    {
        lock(_loggerLock)
        {
            _logWriter.Dispose();
        }
    }
}