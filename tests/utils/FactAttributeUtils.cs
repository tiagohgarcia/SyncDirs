namespace SyncDirs.Tests;

public sealed class SymLinkFactAttribute : FactAttribute
{
    public SymLinkFactAttribute()
    {
        if(!SymLinkAllowed.IsAllowed)
        {
            Skip = "symlink creation is not allowed in this machine";
        }
    }
}

public sealed class FilePermissionFactAttribute : FactAttribute
{
    public FilePermissionFactAttribute()
    {
        if (!FilePermissionUtils.IsAllowed)
            Skip = "requires POSIX permissions for the current user";
    }
}

public sealed class PosixSignalFactAttribute : FactAttribute
{
    public PosixSignalFactAttribute()
    {
        if (OperatingSystem.IsWindows())
            Skip = "SIGINT is not available on Windows";
    }
}