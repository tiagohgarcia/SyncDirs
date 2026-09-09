namespace SyncDirs.Tests;

internal static class SymLinkAllowed
{
    public static bool IsAllowed { get; } = Check();

    private static bool Check()
    {
        try
        {
            using var temp = new TempDirectoryUtils();
            Directory.CreateSymbolicLink(Path.Combine(temp.Path, "test"), temp.Path);
            return true;
        }
        catch
        {
            return false;
        }
    }
}