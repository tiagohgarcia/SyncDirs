using System.Diagnostics;
using Xunit.Abstractions;

namespace SyncDirs.Tests;

public class SyncEnginePerformanceTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public SyncEnginePerformanceTests(ITestOutputHelper testOutputHelper) => _testOutputHelper = testOutputHelper;

    [Fact]
    [Trait("Category", "Performance")]
    public async Task RunCycleAsync_LargeDirectoryTree_SteadyStateCycleIsCheaperThanInitialSync()
    {
        const int dirs = 100;
        const int filesPerDir = 100;
        const int totalFiles = dirs * filesPerDir;

        using var sync = new SyncUtils();

        // test traversal and comparison, not disk bandwidth
        for(int d = 0; d < dirs; d++)
            for(int f = 0; f < filesPerDir; f++)
                sync.WriteInSource(Path.Combine($"dir{d}", $"file{f}.txt"), $"{d}{f}");

        // initial sync
        Stopwatch sw = Stopwatch.StartNew();
        SyncStats initialStats = await sync.Cycle();
        TimeSpan initialTime = sw.Elapsed;

        // steady cycle
        sw.Restart();
        SyncStats steadyStats = await sync.Cycle();
        TimeSpan steadyTime = sw.Elapsed;

        _testOutputHelper.WriteLine($"tree          : {filesPerDir} files per directory. {dirs} directories. Total: {totalFiles} files");
        _testOutputHelper.WriteLine($"initial sync  : {initialTime.TotalMilliseconds:F0} ms ({totalFiles / initialTime.TotalSeconds:F0} files per second)");
        _testOutputHelper.WriteLine($"steady cycle  : {steadyTime.TotalMilliseconds:F0} ms ({totalFiles / steadyTime.TotalSeconds:F0} files per second)");
        _testOutputHelper.WriteLine($"ratio         : {steadyTime.TotalMilliseconds / initialTime.TotalMilliseconds:P1} of initial");

        Assert.Equal(totalFiles, initialStats.FilesCopied);
        Assert.False(steadyStats.HasChanges);
        Assert.Empty(sync.Log.Error);
        Assert.True(steadyTime.TotalMilliseconds < initialTime.TotalMilliseconds * 0.4,  
                    $"steady-state cycle was not meaningfully cheaper than the initial sync (initial {initialTime.TotalMilliseconds:F0} ms, steady {steadyTime.TotalMilliseconds:F0} ms)");
    }

    [Theory]
    [Trait("Category", "Performance")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunCycleAsync_DoubleFileCount_ScalesMostlyLinearly(bool forceHashing)
    {
        var (steadyTimeSmaller, statsSmaller) = await MeasureSteadyCycleAsync(100, 100, forceHashing);
        var (steadyTimeBigger, statsBigger) = await MeasureSteadyCycleAsync(200, 100, forceHashing);

        double factor = steadyTimeBigger.TotalMilliseconds / steadyTimeSmaller.TotalMilliseconds;

        _testOutputHelper.WriteLine($"10,000 files : {steadyTimeSmaller.TotalMilliseconds:F0} ms");
        _testOutputHelper.WriteLine($"20,000 files : {steadyTimeBigger.TotalMilliseconds:F0} ms");
        _testOutputHelper.WriteLine($"factor       : {factor:F2}x for 2x the files");

        Assert.False(statsSmaller.HasChanges);
        Assert.False(statsBigger.HasChanges);
        Assert.True(factor < 3.0, $"scaling is worse than linear: {factor:F2}x for 2x the files");
    }

    private static async Task<(TimeSpan steadyTime, SyncStats stats)> MeasureSteadyCycleAsync(int dirs, int filesPerDir, bool forceHashing)
    {
        using var sync = new SyncUtils();

        for(int d = 0; d < dirs; d++)
            for(int f = 0; f < filesPerDir; f++)
                sync.WriteInSource(Path.Combine($"dir{d}", $"file{f}.txt"), $"{d}{f}");

        await sync.Cycle(); // initial cycle - not measured

        if(forceHashing)
        {
            DateTime touched = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            foreach(string file in Directory.GetFiles(sync.Source, "*", SearchOption.AllDirectories))
                File.SetLastWriteTimeUtc(file, touched);
        }

        Stopwatch sw = Stopwatch.StartNew();
        SyncStats stats = await sync.Cycle();
        TimeSpan steadyTime = sw.Elapsed;
        return (sw.Elapsed, stats);
    }
}