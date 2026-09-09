namespace SyncDirs.Tests;

internal static class FilePermissionUtils
{
    public static bool IsAllowed { get; } = Check();

    private static bool Check()
    {
        if (OperatingSystem.IsWindows()) return false;

        try
        {
            using var temp = new TempDirectoryUtils();
            string dir = Directory.CreateDirectory(Path.Combine(temp.Path, "test")).FullName;
            try
            {
                File.SetUnixFileMode(dir, UnixFileMode.None);
                Directory.GetFileSystemEntries(dir);
                return false; // running as root succeeds - not useful for testing
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
            finally
            {
                File.SetUnixFileMode(dir, Writable);
            }
        }
        catch { 
            return false; 
        }
    }

    public const UnixFileMode Writable  = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    public const UnixFileMode ReadOnly  = UnixFileMode.UserRead | UnixFileMode.UserExecute;
}