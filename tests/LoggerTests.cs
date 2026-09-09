namespace SyncDirs.Tests;

public class LoggerTests
{
    [Theory]
    [InlineData("INFO ")]
    [InlineData("WARN ")]
    [InlineData("ERROR")]
    public void Write_WritesLineToFile_WithCorrectLevel(string level)
    {
        using var temp = new TempDirectoryUtils();
        string path = Path.Combine(temp.Path, "test.log");

        using(var log = new Logger(path)) 
        {
            switch(level)
            {
                case "INFO ":
                    log.Info("test");
                    break;
                case "WARN ":
                    log.Warn("test");
                    break;
                case "ERROR":
                    log.Error("test");
                    break;
            }
            
        }
        
        string line = Assert.Single(File.ReadAllLines(path));
        string pattern = @"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+-]\d{2}:\d{2} \[" + level + @"\] test$";
        Assert.Matches(pattern, line);
    }

    [Fact]
    public void Constructor_ExistingLogFile_Appends()
    {
        using var temp = new TempDirectoryUtils();
        string path = Path.Combine(temp.Path, "test.log");
        
        File.WriteAllText(path, "previous message" + Environment.NewLine);

        using (var log = new Logger(path)) log.Info("new info");

        string[] lines = File.ReadAllLines(path);
        Assert.Equal(2, lines.Length);
        Assert.Equal("previous message", lines[0]);
    }

    [Fact]
    public void Constructor_LogFileInsideNonExistentDirectory_CreatesDirectoryAndFile()
    {
        using var temp = new TempDirectoryUtils();
        string path = Path.Combine(temp.Path, "logdir", "nested", "test.log");

        using (var log = new Logger(path)) log.Info("test");

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Write_AfterDispose_ThrowsOnceAndStaysSilentAfter()
    {
        using var temp = new TempDirectoryUtils();
        string path = Path.Combine(temp.Path, "logdir", "nested", "test.log");

        var log = new Logger(path);
        log.Info("test");
        log.Dispose();

        log.Info("after dispose");
        
        Assert.Single(File.ReadAllLines(path));
    }

    [Fact]
    public async Task Write_AccessedByMultipleThreads_OutputIsWellFormatted()
    {
        const int tasks = 10;
        const int writesPerTask = 20;

        using var temp = new TempDirectoryUtils();
        string path = Path.Combine(temp.Path, "test.log");

        using (var log = new Logger(path))
        {
            await Task.WhenAll(Enumerable.Range(0, tasks).Select(t => Task.Run(() =>
            {
                for(int i = 0; i < writesPerTask; i++)
                    log.Info($"task {t} info {i}");
            })));
        }

        string[] lines = File.ReadAllLines(path);
        Assert.Equal(tasks * writesPerTask, lines.Length);
        for (int i = 0; i < lines.Length; i++)
        {
            Assert.Matches(@"\[INFO \] task \d+ info \d+$", lines[i]);
        }
    }
}