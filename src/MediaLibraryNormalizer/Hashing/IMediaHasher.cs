namespace MediaLibraryNormalizer.Hashing;

/// <summary>
/// Computes partial file hashes for duplicate detection.
/// </summary>
public interface IMediaHasher
{
    /// <summary>
    /// Compute a composite hash key: "{fileSize}:{firstMBHash}:{lastMBHash}".
    /// For files smaller than 2 MB, hashes the entire file.
    /// </summary>
    Task<string> ComputeHashAsync(string filePath);
}
