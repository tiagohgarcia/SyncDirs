namespace SyncDirs.Tests;

public class FileComparerTests
{
    [Fact]
    public async Task AreEqualAsync_DifferentSizes_ReturnsFalse()
    {
        using var temp = new TempDirectoryUtils();
        
        string file1 = Path.Combine(temp.Path, "file1.txt");
        string file2 = Path.Combine(temp.Path, "file2.txt");
        File.WriteAllText(file1, "aaaaa");
        File.WriteAllText(file2, "a"); // different size

        Assert.False(await FileComparer.AreEqualAsync(new FileInfo(file1), new FileInfo(file2), CancellationToken.None));
    }

    [Fact]
    public async Task AreEqualAsync_SameSizeAndSameModificationTimeAndDifferentContent_ReturnsTrue()
    {
        /*
            edge case where files have different content but exactly same size and last write time
        */

        using var temp = new TempDirectoryUtils();
        
        string file1 = Path.Combine(temp.Path, "file1.txt");
        string file2 = Path.Combine(temp.Path, "file2.txt");
        File.WriteAllText(file1, "aaaaa");
        File.WriteAllText(file2, "bbbbb"); // same size but different content

        DateTime mtime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(file1, mtime);
        File.SetLastWriteTimeUtc(file2, mtime); // same mtime

        Assert.True(await FileComparer.AreEqualAsync(new FileInfo(file1), new FileInfo(file2), CancellationToken.None));
    }

    [Fact]
    public async Task AreEqualAsync_SameSizeDifferentModificationTimeSameContent_ReturnsTrue()
    {
        using var temp = new TempDirectoryUtils();
        
        string file1 = Path.Combine(temp.Path, "file1.txt");
        string file2 = Path.Combine(temp.Path, "file2.txt");
        File.WriteAllText(file1, "aaaaa");
        File.WriteAllText(file2, "aaaaa"); // same size and same content
        
        DateTime mtime1 = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime mtime2 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(file1, mtime1);
        File.SetLastWriteTimeUtc(file2, mtime2); // different mtime

        bool output = await FileComparer.AreEqualAsync(new FileInfo(file1), new FileInfo(file2), CancellationToken.None);

        Assert.True(output);
    }

    [Fact]
    public async Task AreEqualAsync_SameSizeDifferentModificationTimeDifferentContent_ReturnsFalse()
    {
        using var temp = new TempDirectoryUtils();
        
        string file1 = Path.Combine(temp.Path, "file1.txt");
        string file2 = Path.Combine(temp.Path, "file2.txt");
        File.WriteAllText(file1, "aaaaa");        
        File.WriteAllText(file2, "bbbbb"); // same size but different content
        
        DateTime mtime1 = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime mtime2 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(file1, mtime1);
        File.SetLastWriteTimeUtc(file2, mtime2); // different mtime

        Assert.False(await FileComparer.AreEqualAsync(new FileInfo(file1), new FileInfo(file2), CancellationToken.None));
    }
}