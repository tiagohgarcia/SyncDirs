namespace SyncDirs.Tests;

public class CommandLineOptionsTests
{
    [Theory]
    [InlineData(new string[] {}, "exactly two")]
    [InlineData(new string[] {"source"}, "exactly two")]
    [InlineData(new string[] {"source", "replica", "/example"}, "exactly two")]
    [InlineData(new string[] {"source", "replica", "--test"}, "unknown option")]
    [InlineData(new string[] {"source", "replica", "-i"}, "invalid value")]
    [InlineData(new string[] {"source", "replica", "-i", "example"}, "must be a positive number")]
    [InlineData(new string[] {"source", "replica", "-i", "0"}, "must be a positive number")]
    [InlineData(new string[] {"source", "replica", "-i", "-l"}, "requires a value")]
    [InlineData(new string[] {"source", "replica", "-l", "  "}, "log path is not valid")]
    public void Parse_ParametersWithInvalidShape_ThrowsArgumentExceptionWithExpectedSubString(string[] args, string expected)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => CommandLineOptions.Parse(args));
        Assert.Contains(expected, exception.Message);

    }

    [Fact]
    public void Parse_WhenSourceDoesNotExist_ThrowsArgumentException()
    {
        using var temp = new TempDirectoryUtils();
        string source = Path.Combine(temp.Path, "nonexistent");

        string[] args = {source, "replica"};
        
        ArgumentException exception = Assert.Throws<ArgumentException>(() => CommandLineOptions.Parse(args));
        Assert.Contains("<source> directory does not exist", exception.Message);
    }

    [Fact]
    public void Parse_WhenSourceIsAFile_ThrowsArgumentException()
    {
        using var temp = new TempDirectoryUtils();
        string source = Path.Combine(temp.Path, "source");
        File.Create(source).Dispose();

        string[] args = {source, "replica"};

        ArgumentException exception = Assert.Throws<ArgumentException>(() => CommandLineOptions.Parse(args));
        Assert.Contains("<source> is a file", exception.Message);
    }

    [Fact]
    public void Parse_WhenReplicaIsAFile_ThrowsArgumentException()
    {
        using var temp = new TempDirectoryUtils();
        string source = Directory.CreateDirectory(Path.Combine(temp.Path, "source")).FullName;
        string replica = Path.Combine(temp.Path, "replica");
        File.Create(replica).Dispose();

        string[] args = {source, replica};

        ArgumentException exception = Assert.Throws<ArgumentException>(() => CommandLineOptions.Parse(args));
        Assert.Contains("<replica> is a file", exception.Message);
    }

    [Fact]
    public void Parse_WhenSourceIsEqualReplica_ThrowsArgumentException()
    {
        using var temp = new TempDirectoryUtils();
        string source = Directory.CreateDirectory(Path.Combine(temp.Path, "source")).FullName;

        string[] args = {source, source};

        ArgumentException exception = Assert.Throws<ArgumentException>(() => CommandLineOptions.Parse(args));
        Assert.Contains("<source> and <replica> cannot be the same folder", exception.Message);
    }

    [Fact]
    public void Parse_WhenReplicaIsSubDirectoryOfSource_ThrowsArgumentException()
    {
        using var temp = new TempDirectoryUtils();
        string source = Directory.CreateDirectory(Path.Combine(temp.Path, "source")).FullName;
        string replica = Directory.CreateDirectory(Path.Combine(source, "replica")).FullName;

        string[] args = {source, replica};

        ArgumentException exception = Assert.Throws<ArgumentException>(() => CommandLineOptions.Parse(args));
        Assert.Contains("cannot be a subfolder", exception.Message);
    }

    [Fact]
    public void Parse_WhenSourceIsSubDirectoryOfReplica_ThrowsArgumentException()
    {
        using var temp = new TempDirectoryUtils();
        string replica = Directory.CreateDirectory(Path.Combine(temp.Path, "replica")).FullName;
        string source = Directory.CreateDirectory(Path.Combine(replica, "source")).FullName;

        string[] args = {source, replica};

        ArgumentException exception = Assert.Throws<ArgumentException>(() => CommandLineOptions.Parse(args));
        Assert.Contains("cannot be a subfolder", exception.Message);
    }

    [Fact]
    public void Parse_WhenLogFileIsInsideSource_ThrowsArgumentException()
    {
        using var temp = new TempDirectoryUtils();
        string source = Directory.CreateDirectory(Path.Combine(temp.Path, "source")).FullName;
        string replica = Directory.CreateDirectory(Path.Combine(temp.Path, "replica")).FullName;
        string logpath = Path.Combine(source, "example.log");
        File.Create(logpath).Dispose();

        string[] args = {source, replica, "-l", logpath};

        ArgumentException exception = Assert.Throws<ArgumentException>(() => CommandLineOptions.Parse(args));
        Assert.Contains("Log file cannot be inside source or replica", exception.Message);
    }

    [Fact]
    public void Parse_WhenLogFileIsInsideReplica_ThrowsArgumentException()
    {
        using var temp = new TempDirectoryUtils();
        string source = Directory.CreateDirectory(Path.Combine(temp.Path, "source")).FullName;
        string replica = Directory.CreateDirectory(Path.Combine(temp.Path, "replica")).FullName;
        string logpath = Path.Combine(replica, "example.log");
        File.Create(logpath).Dispose();

        string[] args = {source, replica, "-l", logpath};

        ArgumentException exception = Assert.Throws<ArgumentException>(() => CommandLineOptions.Parse(args));
        Assert.Contains("Log file cannot be inside source or replica", exception.Message);
    }

    [Fact]
    public void Parse_WhenAllArgumentsPassedAndValid_ReturnsValidObject()
    {
        using var temp = new TempDirectoryUtils();
        string source = Directory.CreateDirectory(Path.Combine(temp.Path, "source")).FullName;
        string replica = Directory.CreateDirectory(Path.Combine(temp.Path, "replica")).FullName;
        string logpath = Path.Combine(temp.Path, "example.log");
        File.Create(logpath).Dispose();

        string[] args = {source, replica, "-i", "120", "-l", logpath};

        CommandLineOptions options = CommandLineOptions.Parse(args);

        Assert.True(PathHelper.AreEqual(PathHelper.GetRealPath(source), options.SourcePath));
        Assert.True(PathHelper.AreEqual(PathHelper.GetRealPath(replica), options.ReplicaPath));
        Assert.True(PathHelper.AreEqual(PathHelper.GetRealPath(logpath), options.LogPath));
        Assert.Equal(TimeSpan.FromSeconds(120), options.Interval);
    }

    [Fact]
    public void Parse_WhenAllArgumentsPassedAreValidButInDifferentOrder_ReturnsValidObject()
    {
        using var temp = new TempDirectoryUtils();
        string source = Directory.CreateDirectory(Path.Combine(temp.Path, "source")).FullName;
        string replica = Directory.CreateDirectory(Path.Combine(temp.Path, "replica")).FullName;
        string logpath = Path.Combine(temp.Path, "example.log");
        File.Create(logpath).Dispose();

        string[] args = {"--interval", "120", "--logpath", logpath, source, replica};

        CommandLineOptions options = CommandLineOptions.Parse(args);

        Assert.True(PathHelper.AreEqual(PathHelper.GetRealPath(source), options.SourcePath));
        Assert.True(PathHelper.AreEqual(PathHelper.GetRealPath(replica), options.ReplicaPath));
        Assert.True(PathHelper.AreEqual(PathHelper.GetRealPath(logpath), options.LogPath));
        Assert.Equal(TimeSpan.FromSeconds(120), options.Interval);
    }

    [Fact]
    public void Parse_WhenIntervalAndLogPathAreDefault_ReturnsValidObject()
    {
        using var temp = new TempDirectoryUtils();
        string source = Directory.CreateDirectory(Path.Combine(temp.Path, "source")).FullName;
        string replica = Directory.CreateDirectory(Path.Combine(temp.Path, "replica")).FullName;

        string[] args = {source, replica};

        CommandLineOptions options = CommandLineOptions.Parse(args);

        Assert.True(PathHelper.AreEqual(PathHelper.GetRealPath(source), options.SourcePath));
        Assert.True(PathHelper.AreEqual(PathHelper.GetRealPath(replica), options.ReplicaPath));
        Assert.True(PathHelper.AreEqual(PathHelper.GetRealPath("syncdirs.log"), options.LogPath)); // default log path
        Assert.Equal(TimeSpan.FromSeconds(60), options.Interval); // default interval
    }
}

