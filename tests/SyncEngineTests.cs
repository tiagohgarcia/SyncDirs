namespace SyncDirs.Tests;

public class SyncEngineTests
{
    private sealed class SyncHelper : IDisposable
    {
        private readonly TempDirectoryUtils _temp = new();

        public string Source { get; }
        public string Replica { get; }
        public LoggerUtils Log { get; } = new();
        public SyncEngine Engine { get; }

        public SyncHelper()
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

    [Fact]
    public async Task RunCycleAsync_NewFileInSource_IsCopiedWithSameContentAndModificationTime()
    {
        using var sync = new SyncHelper();
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
        using var sync = new SyncHelper();
        sync.WriteInSource("data.txt", "test");
        sync.WriteInSource("sub/example.txt", "example");

        await sync.Cycle();
        SyncStats second = await sync.Cycle();

        Assert.False(second.HasChanges);
        Assert.Empty(sync.Log.Error);
    }

    [Fact]
    public async Task RunCycleAsync_FileDeletedFromSource_IsDeletedFromReplica()
    {
        using var sync = new SyncHelper();
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
        using var sync = new SyncHelper();
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
        using var sync = new SyncHelper();
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
        using var sync = new SyncHelper();
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
        using var sync = new SyncHelper();
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

        using var sync = new SyncHelper();
        string subSourceDir = Directory.CreateDirectory(Path.Combine(sync.Source, "sub")).FullName;
        Directory.CreateSymbolicLink(Path.Combine(subSourceDir, "loop"), sync.Source); // looped symlink

        await sync.Engine.RunCycleAsync(cts.Token); // cancellation token in case guard fails (infinite loop)

        Assert.NotEmpty(sync.Log.Warn); // guard fired "symlink cycle detected"
    }
}