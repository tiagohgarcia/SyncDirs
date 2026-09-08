namespace SyncDirs.Tests;

internal sealed class TempDirectoryUtils : IDisposable
{
    public string Path { get; }
    public TempDirectoryUtils()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }
    public void Dispose() => Directory.Delete(Path, recursive: true);
}