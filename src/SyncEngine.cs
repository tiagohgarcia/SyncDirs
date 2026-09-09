namespace SyncDirs;

internal sealed class SyncEngine
{
    private readonly string _sourceRoot;
    private readonly string _replicaRoot;
    private readonly ISyncLogger _log;

    public SyncEngine(string source, string replica, ISyncLogger log)
    {
        _sourceRoot = source;
        _replicaRoot = replica;
        _log = log;     
    }

    public async Task<SyncStats> RunCycleAsync(CancellationToken ct)
    {
        SyncStats stats = new();
        HashSet<string> visited = new HashSet<string>(PathHelper.Comparer);

        await SyncDirectoryAsync(new DirectoryInfo(_sourceRoot), _replicaRoot, visited, stats, ct);

        return stats;
    }

    private async Task SyncDirectoryAsync(DirectoryInfo sourceDir, string replicaDir, HashSet<string> visited, SyncStats stats, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        string realSourceDir = PathHelper.GetRealPath(sourceDir.FullName);
        if(!visited.Add(realSourceDir))
        {
            _log.Warn($"symlink cycle detected: skipping {sourceDir.FullName}");
            return;
        }

        try
        {
            if(File.Exists(replicaDir))
            {
                _log.Warn($"replacing file with directory: {PathHelper.Relative(_replicaRoot, replicaDir)}");
                DeleteEntry(new FileInfo(replicaDir), stats);
            }
            if(!Directory.Exists(replicaDir))
            {
                Directory.CreateDirectory(replicaDir);
                _log.Info($"created directory: {PathHelper.Relative(_replicaRoot, replicaDir)}");
                stats.DirectoriesCreated++;
            }
        
            // Check all source entries and verify if it needs updating on replica side
            FileSystemInfo[] sourceEntries = sourceDir.GetFileSystemInfos();
            HashSet<string> sourceEntriesSet = new HashSet<string>(sourceEntries.Select(ent => ent.Name), PathHelper.Comparer);

            foreach(FileSystemInfo entry in sourceEntries)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    string replicaPath = Path.Combine(replicaDir, entry.Name);
                    if(entry is FileInfo)
                    {
                        bool update = false;
                        if(File.Exists(replicaPath))
                        {
                            if(await FileComparer.AreEqualAsync(new FileInfo(entry.FullName), new FileInfo(replicaPath), ct)) continue; // if files are equal: skip
                                
                            update = true;
                        }
                        if(Directory.Exists(replicaPath))
                        {
                            _log.Warn($"replacing directory with file {PathHelper.Relative(_replicaRoot, replicaPath)}");
                            DeleteEntry(new DirectoryInfo(replicaPath), stats);
                        }
                        
                        DateTime sourceTime = new FileInfo(entry.FullName).LastWriteTimeUtc;
                        File.Copy(entry.FullName, replicaPath, overwrite: true);
                        File.SetLastWriteTimeUtc(replicaPath, sourceTime); // replica and source files must have same modification time
                        if(update)
                        {
                            _log.Info($"updated file: {PathHelper.Relative(_replicaRoot, replicaPath)}");
                            stats.FilesUpdated++;
                        }
                        else
                        {
                            _log.Info($"copied file: {PathHelper.Relative(_replicaRoot, replicaPath)}");
                            stats.FilesCopied++;
                        }   
                    }
                    else if(entry is DirectoryInfo dir)
                    {
                        await SyncDirectoryAsync(dir, replicaPath, visited, stats, ct);
                    }
                }
                catch(Exception e)
                {
                    _log.Error($"failed to process {entry.FullName}: {e.Message}");
                    stats.Errors++;
                }
            }

            // Check for entries that only exist in replica but not in source
            DirectoryInfo dirInfo = new DirectoryInfo(replicaDir);
            FileSystemInfo[] replicaEntries = dirInfo.GetFileSystemInfos();

            foreach(FileSystemInfo entry in replicaEntries)
            {
                ct.ThrowIfCancellationRequested();
                if(sourceEntriesSet.Contains(entry.Name)) continue;

                try
                {
                    DeleteEntry(entry, stats);
                }
                catch(Exception e)
                {
                    _log.Error($"failed to delete: {entry.FullName}: {e.Message}");
                    stats.Errors++;
                }
            }
        }
        finally
        {
            visited.Remove(realSourceDir);
        }
    }

    private void DeleteEntry(FileSystemInfo entry, SyncStats stats)
    {
        string entryPath = PathHelper.Relative(_replicaRoot, entry.FullName);

        // symlink - remove the link, not follow it
        if(entry.LinkTarget is not null)
        {
            if(entry is DirectoryInfo dirLink)
                dirLink.Delete(recursive: false);
            else
                entry.Delete();

            stats.SymLinksDeleted++;
            _log.Info($"deleted symlink: {entryPath}");
            return;
        }

        // directory
        if(entry is DirectoryInfo dir)
        {
            foreach(FileSystemInfo child in dir.GetFileSystemInfos())
            {
                try
                {
                    DeleteEntry(child, stats);
                }
                catch(Exception e)
                {
                    _log.Error($"failed to delete {child.FullName}: {e.Message}");
                    stats.Errors++;
                }
            }

            dir.Delete(recursive: false);
            stats.DirectoriesDeleted++;
            _log.Info($"deleted directory: {entryPath}");
            return;
        }

        // File
        entry.Delete();
        stats.FilesDeleted++;
        _log.Info($"deleted file: {entryPath}");
    }
}