using MediaLibraryNormalizer.Config;
using MediaLibraryNormalizer.Hashing;
using Microsoft.Extensions.Logging.Abstractions;

namespace MediaLibraryNormalizer.Tests.Hashing;

public class MediaHasherTests : IDisposable
{
    private readonly string _tempDir;
    private readonly MediaHasher _sut;

    public MediaHasherTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MediaHasherTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);

        var config = new NormalizerConfig { HashSizeMB = 1 };
        _sut = new MediaHasher(config, NullLogger<MediaHasher>.Instance);
    }

    [Fact]
    public async Task ComputeHashAsync_SmallFile_ReturnsConsistentHash()
    {
        var filePath = CreateTempFile(1024); // 1 KB

        var hash1 = await _sut.ComputeHashAsync(filePath);
        var hash2 = await _sut.ComputeHashAsync(filePath);

        Assert.NotEmpty(hash1);
        Assert.Equal(hash1, hash2); // Same file → same hash
    }

    [Fact]
    public async Task ComputeHashAsync_DifferentFiles_DifferentHashes()
    {
        var file1 = CreateTempFile(1024, fillByte: 0xAA);
        var file2 = CreateTempFile(1024, fillByte: 0xBB);

        var hash1 = await _sut.ComputeHashAsync(file1);
        var hash2 = await _sut.ComputeHashAsync(file2);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public async Task ComputeHashAsync_LargeFile_UsesPartialHashing()
    {
        // Create a file larger than 2 MB (2 * HashBlockSize)
        var filePath = CreateTempFile(3 * 1024 * 1024); // 3 MB

        var hash = await _sut.ComputeHashAsync(filePath);

        Assert.NotEmpty(hash);
        // Hash format: "fileSize:firstBlockHash:lastBlockHash"
        var parts = hash.Split(':');
        Assert.Equal(3, parts.Length);
        Assert.Equal((3 * 1024 * 1024).ToString(), parts[0]);
    }

    [Fact]
    public async Task ComputeHashAsync_SmallFile_SameHashForBothParts()
    {
        // Small file (< 2*blockSize) should use full file hash
        var filePath = CreateTempFile(500); // 500 bytes

        var hash = await _sut.ComputeHashAsync(filePath);

        var parts = hash.Split(':');
        Assert.Equal(3, parts.Length);
        Assert.Equal("500", parts[0]);
        // For small files, first and last hash should be the same (full file hashed)
        Assert.Equal(parts[1], parts[2]);
    }

    [Fact]
    public async Task ComputeHashAsync_FileNotFound_Throws()
    {
        var nonExistentPath = Path.Combine(_tempDir, "nonexistent.mkv");

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _sut.ComputeHashAsync(nonExistentPath));
    }

    private string CreateTempFile(int sizeBytes, byte fillByte = 0x42)
    {
        var filePath = Path.Combine(_tempDir, $"test_{Guid.NewGuid():N}.bin");
        var data = new byte[sizeBytes];
        Array.Fill(data, fillByte);

        // Add some variation based on position for more realistic hashing
        for (var i = 0; i < data.Length; i += 1024)
        {
            data[i] = (byte)(i % 256);
        }

        File.WriteAllBytes(filePath, data);
        return filePath;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup
        }
    }
}
