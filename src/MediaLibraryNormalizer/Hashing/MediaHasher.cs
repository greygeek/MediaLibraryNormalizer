using System.IO.Hashing;
using MediaLibraryNormalizer.Config;
using Microsoft.Extensions.Logging;

namespace MediaLibraryNormalizer.Hashing;

/// <summary>
/// Partial-file hasher using xxHash64.
/// Hashes the first N MB and last N MB of the file, combined with file size.
/// </summary>
public class MediaHasher(NormalizerConfig config, ILogger<MediaHasher> logger) : IMediaHasher
{
    private int HashBlockSize => config.HashSizeMB * 1024 * 1024; // default 1 MB

    public async Task<string> ComputeHashAsync(string filePath)
    {
        var fi = new FileInfo(filePath);
        if (!fi.Exists)
            throw new FileNotFoundException("File not found for hashing", filePath);

        var fileSize = fi.Length;
        var blockSize = HashBlockSize;

        if (fileSize <= blockSize * 2)
        {
            // Small file — hash everything
            var fullHash = await HashFullFileAsync(filePath);
            return $"{fileSize}:{fullHash}:{fullHash}";
        }

        // Hash first block
        var firstHash = await HashBlockAsync(filePath, 0, blockSize);

        // Hash last block
        var lastHash = await HashBlockAsync(filePath, fileSize - blockSize, blockSize);

        var key = $"{fileSize}:{firstHash}:{lastHash}";

        logger.LogDebug("Hash for {File}: {Key}", Path.GetFileName(filePath), key);
        return key;
    }

    private static async Task<string> HashBlockAsync(string filePath, long offset, int count)
    {
        var buffer = new byte[count];
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: count, useAsync: true);

        stream.Seek(offset, SeekOrigin.Begin);
        var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, count));

        var hash = new XxHash64();
        hash.Append(buffer.AsSpan(0, bytesRead));
        return hash.GetCurrentHashAsUInt64().ToString("X16");
    }

    private static async Task<string> HashFullFileAsync(string filePath)
    {
        var bytes = await File.ReadAllBytesAsync(filePath);
        var hash = new XxHash64();
        hash.Append(bytes);
        return hash.GetCurrentHashAsUInt64().ToString("X16");
    }
}
