namespace SyncDirs.Tests;

internal sealed class SyncUtils : IDisposable
{
    private readonly TempDirectoryUtils _temp = new();

    public string Source { get; }
    public string Replica { get; }
    public LoggerUtils Log { get; } = new();
    public SyncEngine Engine { get; }

    public SyncUtils()
    {
        Source  = Directory.CreateDirectory(Path.Combine(_temp.Path, "source")).FullName;
        Replica = Directory.CreateDirectory(Path.Combine(_temp.Path, "replica")).FullName;
        Engine  = new SyncEngine(Source, Replica, Log);
    }

    // helper to create files in source directory
    public string WriteInSource(string relativePath, string content)
    {
        string fullPath = Path.Combine(Source, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    public Task<SyncStats> Cycle() => Engine.RunCycleAsync(CancellationToken.None);

    public void Dispose() => _temp.Dispose();
}