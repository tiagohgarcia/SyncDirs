namespace SyncDirs;

internal static class PathHelper
{
    public static StringComparison Comparison { get; } = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() 
                                                            ? StringComparison.OrdinalIgnoreCase 
                                                            : StringComparison.Ordinal;
    public static StringComparer Comparer { get; } = StringComparer.FromComparison(Comparison);

    // Get full path + trim separator
    public static string NormalizePath(string path)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        // better than TrimEnd('/', '\\') for root path (ex: C:\\)
    }
    
    // Returns the normalized real path -> resolves symlink directory to real target
    public static string GetRealPath(string path)
    {
        try
        {
            DirectoryInfo di = new DirectoryInfo(path);
            FileSystemInfo? linkTarget = di.ResolveLinkTarget(true);
            if(linkTarget == null)
            {
                return NormalizePath(path);
            }
            return NormalizePath(linkTarget.FullName);
        }
        catch (IOException)
        {
            return NormalizePath(path);
        }
    }

    public static bool IsInside(string child, string parent)
    {
        DirectoryInfo p = new DirectoryInfo(NormalizePath(parent));
        DirectoryInfo c = new DirectoryInfo(NormalizePath(child));

        while(c.Parent != null)
        {
            if(AreEqual(c.Parent.FullName, p.FullName))
            {
                return true;
            }
            c = c.Parent;
        }

        return false;
    }

    public static bool AreEqual(string path1, string path2) => AreEqual(path1, path2, Comparison);
    public static bool AreEqual(string path1, string path2, StringComparison comparison)
    {
        return string.Equals(path1, path2, comparison);
    }

    public static string Relative(string root, string path)
    {
        string relativePath = Path.GetRelativePath(root, path);
        return relativePath == "." ? root : relativePath;
    }
}