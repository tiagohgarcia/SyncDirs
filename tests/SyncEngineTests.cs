namespace SyncDirs.Tests;

public class SyncEngineTests
{
    [Fact]
    public async Task RunCycleAsync_NewFileInSource_IsCopiedWithSameContentAndModificationTime()
    {
        using var sync = new SyncUtils();
        string sourceFile = sync.WriteInSource("data.txt", "test");

        SyncStats stats = await sync.Cycle();
        
        string replicaFile = Path.Combine(sync.Replica, "data.txt");

        Assert.Equal(1, stats.FilesCopied);
        Assert.Equal("test", File.ReadAllText(replicaFile)); // content must match in replica
        Assert.Equal(File.GetLastWriteTimeUtc(sourceFile), File.GetLastWriteTimeUtc(replicaFile)); // mtime matches
        Assert.Empty(sync.Log.Error);
    }

    [Fact]
    public async Task RunCycleAsync_SecondCycle_NoChanges()
    {
        using var sync = new SyncUtils();
        sync.WriteInSource("data.txt", "test");
        sync.WriteInSource("sub/example.txt", "example");

        await sync.Cycle();
        SyncStats second = await sync.Cycle();

        Assert.False(second.HasChanges);
        Assert.Empty(sync.Log.Error);
    }

    [Fact]
    public async Task RunCycleAsync_ModifiedFileInSource_IsUpdatedInReplica()
    {
        using var sync = new SyncUtils();
        string sourceFile = sync.WriteInSource("data.txt", "original");
        
        await sync.Cycle();

        File.WriteAllText(sourceFile, "modified content");
        
        SyncStats stats = await sync.Cycle();

        Assert.Equal(1, stats.FilesUpdated);
        Assert.Equal(0, stats.FilesCopied);
        Assert.Equal("modified content", File.ReadAllText(Path.Combine(sync.Replica, "data.txt")));
    }

    [Fact]
    public async Task RunCycleAsync_FileDeletedFromSource_IsDeletedFromReplica()
    {
        using var sync = new SyncUtils();
        string sourceFile = sync.WriteInSource("data.txt", "test");
        
        await sync.Cycle();
        string replicaFile = Path.Combine(sync.Replica, "data.txt");

        File.Delete(sourceFile);
        
        SyncStats stats = await sync.Cycle();

        Assert.Equal(1, stats.FilesDeleted);
        Assert.False(File.Exists(replicaFile));
        Assert.Empty(sync.Log.Error);
    }

    [Fact]
    public async Task RunCycleAsync_NestedAndEmptyDirectories_AreCreatedOnReplica()
    {
        using var sync = new SyncUtils();
        Directory.CreateDirectory(Path.Combine(sync.Source, "empty"));
        sync.WriteInSource(Path.Combine("sub", "subsub", "nested.txt"), "nested");

        await sync.Cycle();
        string emptyReplicaDir = Path.Combine(sync.Replica, "empty");
        string nestedReplicaFile = Path.Combine(sync.Replica, "sub", "subsub", "nested.txt");

        Assert.True(Directory.Exists(emptyReplicaDir)); 
        Assert.True(File.Exists(nestedReplicaFile));
        Assert.Empty(sync.Log.Error);
    }

    [Fact]
    public async Task RunCycleAsync_ReplicaHasDirectoryWithSameNameOfSourceFile_IsReplaced()
    {
        using var sync = new SyncUtils();
        sync.WriteInSource("same", "this is a file");
        Directory.CreateDirectory(Path.Combine(sync.Replica, "same")); // conflict; file in source with same name as dir in replica

        await sync.Cycle();
        string replicaEntry = Path.Combine(sync.Replica, "same");

        Assert.True(File.Exists(replicaEntry)); // now replica should have a file with that name and not a dir
        Assert.Equal("this is a file", File.ReadAllText(replicaEntry));
        Assert.Empty(sync.Log.Error);
    }

    [Fact]
    public async Task RunCycleAsync_ReplicaHasFileWithSameNameOfSourceDirectory_IsReplaced()
    {
        using var sync = new SyncUtils();
        sync.WriteInSource(Path.Combine("same", "example.txt"), "test");
        File.WriteAllText(Path.Combine(sync.Replica, "same"), "this is a file"); // conflict: dir in source with same name as file in replica

        await sync.Cycle();
        string newReplicaDir = Path.Combine(sync.Replica, "same");

        Assert.True(Directory.Exists(newReplicaDir)); // now replica should have a dir with that name and not a file
        Assert.Equal("test", File.ReadAllText(Path.Combine(newReplicaDir, "example.txt"))); // and a nested file like source
        Assert.Empty(sync.Log.Error);
    }

    [Fact]
    public async Task RunCycleAsync_TouchedButUnchangedFile_IsNotRecopied()
    {
        using var sync = new SyncUtils();
        string sourceFile = sync.WriteInSource("data.txt", "test");
        
        await sync.Cycle();

        File.SetLastWriteTimeUtc(sourceFile, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        SyncStats stats = await sync.Cycle();

        Assert.Equal(0, stats.FilesUpdated); // MD5 hash is identical, no copy  
        Assert.False(stats.HasChanges);
        Assert.Empty(sync.Log.Error);      
    }

    [SymLinkFact]
    public async Task RunCycleAsync_SymlinkCycleInSource_CompletesWithoutHanging()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        using var sync = new SyncUtils();
        string subSourceDir = Directory.CreateDirectory(Path.Combine(sync.Source, "sub")).FullName;
        Directory.CreateSymbolicLink(Path.Combine(subSourceDir, "loop"), sync.Source); // looped symlink

        await sync.Engine.RunCycleAsync(cts.Token); // cancellation token in case guard fails (infinite loop)

        Assert.NotEmpty(sync.Log.Warn); // guard fired "symlink cycle detected"
    }

    [Fact]
    public async Task RunCycleAsync_DirectoryRemovedFromSource_IsDeletedFromReplicaWithContents()
    {
        using var sync = new SyncUtils();
        sync.WriteInSource(Path.Combine("data", "test.txt"), "aaaa");
        sync.WriteInSource(Path.Combine("data", "nested", "inside.txt"), "bbbb");
        
        await sync.Cycle();

        Directory.Delete(Path.Combine(sync.Source, "data"), recursive: true);
        
        SyncStats stats = await sync.Cycle();

        Assert.False(Directory.Exists(Path.Combine(sync.Replica, "data")));
        Assert.Equal(2, stats.FilesDeleted);
        Assert.Equal(2, stats.DirectoriesDeleted);
        Assert.Empty(sync.Log.Error);
    }

    [SymLinkFact]
    public async Task RunCycleAsync_OnlyReplicaHasSymlinkToFile_SymLinkIsDeletedButTargetFileIsNot()
    {
        using var outside = new TempDirectoryUtils();
        string target = Path.Combine(outside.Path, "target.txt");
        File.WriteAllText(target, "cannot be deleted");

        using var sync = new SyncUtils();
        File.CreateSymbolicLink(Path.Combine(sync.Replica, "link.txt"), target); // link is on replica only
        
        SyncStats stats = await sync.Cycle();

        Assert.False(File.Exists(Path.Combine(sync.Replica, "link.txt")));
        Assert.True(File.Exists(target));
        Assert.Equal(1, stats.SymLinksDeleted);
        Assert.Empty(sync.Log.Error);
    }

    [SymLinkFact]
    public async Task RunCycleAsync_OnlyReplicaHasSymlinkToDirectory_SymLinkIsDeletedButTargetDirectoryIsNot()
    {
        using var outside = new TempDirectoryUtils();
        string target = Path.Combine(outside.Path, "target");
        Directory.CreateDirectory(target);

        using var sync = new SyncUtils();
        Directory.CreateSymbolicLink(Path.Combine(sync.Replica, "link"), target); // link is on replica only
        
        SyncStats stats = await sync.Cycle();

        Assert.False(Directory.Exists(Path.Combine(sync.Replica, "link")));
        Assert.True(Directory.Exists(target));
        Assert.Equal(1, stats.SymLinksDeleted);
        Assert.Empty(sync.Log.Error);
    }

    [FilePermissionFact]
    public async Task RunCycleAsync_CannotReadSourceSubDirectory_LogsErrorAndContinues()
    {
        using var sync = new SyncUtils();
        sync.WriteInSource(Path.Combine("readable", "test.txt"), "can read");
        sync.WriteInSource(Path.Combine("blocked", "secret.txt"), "secret");
        string blocked = Path.Combine(sync.Source, "blocked");

        SyncStats stats;
        try
        {
            File.SetUnixFileMode(blocked, UnixFileMode.None);
            
            stats = await sync.Cycle();
        }
        finally
        {
            File.SetUnixFileMode(blocked, FilePermissionUtils.Writable);
        }

        Assert.True(File.Exists(Path.Combine(sync.Replica, "readable", "test.txt")));
        Assert.Equal(1, stats.Errors);
        Assert.NotEmpty(sync.Log.Error);
    }

    [FilePermissionFact]
    public async Task RunCycleAsync_ReplicaEntryCannotBeDeleted_LogsErrorAndContinues()
    {
        using var sync = new SyncUtils();
        File.WriteAllText(Path.Combine(sync.Replica, "stale.txt"), "should be deleted");

        SyncStats stats;
        try
        {
            File.SetUnixFileMode(sync.Replica, FilePermissionUtils.ReadOnly);   // cannot delete
            
            stats = await sync.Cycle();
        }
        finally
        {
            File.SetUnixFileMode(sync.Replica, FilePermissionUtils.Writable);
        }

        Assert.True(File.Exists(Path.Combine(sync.Replica, "stale.txt"))); // file still exists
        Assert.Equal(1, stats.Errors);
        Assert.NotEmpty(sync.Log.Error);
    }

    [FilePermissionFact]
    public async Task RunCycleAsync_DirectoryChildCannotBeDeleted_LogsErrorAndContinues()
    {
        using var sync = new SyncUtils();
        string staleDir = Directory.CreateDirectory(Path.Combine(sync.Replica, "stale")).FullName;
        File.WriteAllText(Path.Combine(staleDir, "test.txt"), "stale");

        SyncStats stats;
        try
        {
            File.SetUnixFileMode(staleDir, FilePermissionUtils.ReadOnly);
            stats = await sync.Cycle();
        }
        finally
        {
            File.SetUnixFileMode(staleDir, FilePermissionUtils.Writable);
        }

        Assert.True(Directory.Exists(staleDir));
        Assert.True(stats.Errors == 2);
        Assert.NotEmpty(sync.Log.Error);
    }
}