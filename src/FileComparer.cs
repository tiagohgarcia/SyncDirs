using System.Security.Cryptography;

namespace SyncDirs;

internal static class FileComparer
{
    public static async Task<bool> AreEqualAsync(FileInfo source, FileInfo replica, CancellationToken ct)
    {
        if(source.Length != replica.Length)
            return false;

        
        if(source.LastWriteTimeUtc == replica.LastWriteTimeUtc)
            return true;


        FileStreamOptions options = new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan // dont polute cache
        };

        await using var stream1 = new FileStream(source.FullName, options);
        byte[] sourceHash = await MD5.HashDataAsync(stream1, ct);

        await using var stream2 = new FileStream(replica.FullName, options);
        byte[] replicaHash = await MD5.HashDataAsync(stream2, ct);

        return sourceHash.AsSpan().SequenceEqual(replicaHash);
    }
}