namespace SyncDirs.Tests;

public class PathHelperTests
{
    [Theory]
    [InlineData("/data/example/")] // "/data/example//"
    [InlineData("/data/example/sub")] // "/data/example/sub/"
    public void NormalizePath_PathWithDirectorySeparator_IsTrimmed(string path)
    {
        Assert.Equal(PathHelper.NormalizePath(path), PathHelper.NormalizePath(path + Path.DirectorySeparatorChar));
    }

    [Fact]
    public void NormalizePath_DotDot_Collapses()
    {
        Assert.Equal(PathHelper.NormalizePath("/data/new"), PathHelper.NormalizePath("/data/example/../new"));
    }

    [Fact]
    public void NormalizePath_Root_IsPreserved()
    {
        string? root = Path.GetPathRoot(Path.GetFullPath(".")); // Linux : '/'  Windows : "C:\"
        Assert.Equal(root, PathHelper.NormalizePath(root!));
    }

    [Theory]
    [InlineData("/data", "/", true)]
    [InlineData("/data/example/sub", "/data/example", true)]
    [InlineData("/data/example/new.txt", "/data/example", true)]
    [InlineData("/data/example/sub/subsub/subsubsub", "/data/example", true)]
    [InlineData("/data/example", "/data/example", false)]
    [InlineData("/data/example", "/data/test", false)]
    [InlineData("/data/example", "/data/example/sub", false)]
    public void IsInside_WithDifferentPaths_ReturnsExpectedResult(string child, string parent, bool expected)
    {
        Assert.Equal(expected, PathHelper.IsInside(child, parent));
    }

    [Theory]
    [InlineData("/data", "/", StringComparison.Ordinal, false)]
    [InlineData("/data", "/data", StringComparison.Ordinal, true)]
    [InlineData("/data", "/data", StringComparison.OrdinalIgnoreCase, true)]
    [InlineData("/data", "/Data", StringComparison.Ordinal, false)]
    [InlineData("/data", "/Data", StringComparison.OrdinalIgnoreCase, true)]
    public void AreEqual_WithDifferentPaths_ReturnsExpectedResult(string path1, string path2, StringComparison comparison, bool expected)
    {
        Assert.Equal(expected, PathHelper.AreEqual(path1, path2, comparison));
    }

    [Fact]
    public void GetRealPath_NormalDirectory_ReturnsPath()
    {
        using var temp = new TempDirectoryUtils();
        string data = Directory.CreateDirectory(Path.Combine(temp.Path, "data")).FullName;

        Assert.Equal(data, PathHelper.GetRealPath(data));
    }

    [SymLinkFact]
    public void GetRealPath_LinkToDirectory_ReturnsTarget()
    {
        using var temp = new TempDirectoryUtils();
        string linkPath = Path.Combine(temp.Path, "linkdata");
        string targetPath = Directory.CreateDirectory(Path.Combine(temp.Path, "targetdata")).FullName;
        Directory.CreateSymbolicLink(linkPath, targetPath);

        Assert.Equal(targetPath, PathHelper.GetRealPath(linkPath));
    }
    
    [SymLinkFact]
    public void GetRealPath_LinkToLinkToDirectory_ReturnsFinalTarget()
    {
        using var temp = new TempDirectoryUtils();
        string linklinkPath = Path.Combine(temp.Path, "linklinkdata");
        string linkPath = Path.Combine(temp.Path, "linkdata");
        string targetPath = Directory.CreateDirectory(Path.Combine(temp.Path, "targetdata")).FullName;
        Directory.CreateSymbolicLink(linklinkPath, linkPath);
        Directory.CreateSymbolicLink(linkPath, targetPath);

        Assert.Equal(targetPath, PathHelper.GetRealPath(linklinkPath));
    }

    [SymLinkFact]
    public void GetRealPath_DoubleLinked_ReturnsInputPath()
    {
        using var temp = new TempDirectoryUtils();
        string linkPath = Path.Combine(temp.Path, "linkdata");
        string targetPath = Path.Combine(temp.Path, "targetdata"); // both must be links
        Directory.CreateSymbolicLink(linkPath, targetPath);
        Directory.CreateSymbolicLink(targetPath, linkPath);

        Assert.Equal(linkPath, PathHelper.GetRealPath(linkPath));
    }

    [SymLinkFact]
    public void GetRealPath_NonExistentTarget_ReturnsResolvedTarget()
    {
        using var temp = new TempDirectoryUtils();
        string linkPath = Path.Combine(temp.Path, "linkdata");
        Directory.CreateSymbolicLink(linkPath, "nonexistent");

        Assert.Equal(Path.Combine(temp.Path, "nonexistent"), PathHelper.GetRealPath(linkPath));
    }
}
