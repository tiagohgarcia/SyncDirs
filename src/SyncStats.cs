namespace SyncDirs;

internal sealed class SyncStats
{
    public int DirectoriesCreated { get; set; }
    public int DirectoriesDeleted { get; set; }
    public int FilesCopied { get; set; }
    public int FilesUpdated { get; set; }
    public int FilesDeleted { get; set; }
    public int SymLinksDeleted { get; set; }
    public int Errors { get; set; }

    public bool HasChanges => DirectoriesCreated 
                            + DirectoriesDeleted 
                            + FilesCopied 
                            + FilesUpdated 
                            + FilesDeleted
                            + SymLinksDeleted
                            + Errors > 0;

    public override string ToString()
    {
        return $"created {DirectoriesCreated} dir(s), copied {FilesCopied} file(s), updated {FilesUpdated} file(s) "
                + $"deleted {DirectoriesDeleted} dir(s), deleted {FilesDeleted} file(s), deleted {SymLinksDeleted} symlink(s) "
                + $"{Errors} error(s)";
    }
}