using MediaLibraryNormalizer.Models;
using MediaLibraryNormalizer.Scanner;
using Microsoft.Extensions.Logging;

namespace MediaLibraryNormalizer.Merging;

/// <summary>
/// Safe file mover with retry logic, associated file handling, and transaction logging.
/// </summary>
public class FileMover(
    IMediaFileDetector fileDetector,
    ITransactionLog transactionLog,
    ILogger<FileMover> logger) : IFileMover
{
    private const int MaxRetries = 3;
    private const int CopyBufferSize = 1024 * 1024; // 1 MiB
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);

    public IProgress<string>? FileTransferProgress { get; set; }
    public CancellationToken CancellationToken { get; set; }

    public async Task<List<MergeOperation>> MoveFileAsync(string source, string destination, bool dryRun)
    {
        var operations = new List<MergeOperation>();

        // Ensure destination directory exists
        var destDir = Path.GetDirectoryName(destination)!;
        if (!Directory.Exists(destDir))
        {
            var createOp = new MergeOperation(destDir, destDir, OperationType.CreateDirectory, dryRun);
            operations.Add(createOp);

            if (!dryRun)
            {
                Directory.CreateDirectory(destDir);
                await transactionLog.LogAsync(createOp);
            }
        }

        // Check cancellation before starting a new file (current file always completes atomically).
        CancellationToken.ThrowIfCancellationRequested();

        // Move the video file
        var moveOp = new MergeOperation(source, destination, OperationType.Move, dryRun);
        operations.Add(moveOp);

        if (!dryRun)
        {
            var diagPath = Path.Combine(Path.GetPathRoot(source) ?? ".", "TV", "merge-diagnostic.log");
            File.AppendAllText(diagPath, $"{DateTime.UtcNow:o}  EXEC File.Move: {source} → {destination}{Environment.NewLine}");
            await MoveWithRetryAsync(source, destination);
            File.AppendAllText(diagPath, $"{DateTime.UtcNow:o}  DONE File.Move: src_exists={File.Exists(source)} dst_exists={File.Exists(destination)}{Environment.NewLine}");
            await transactionLog.LogAsync(moveOp);
        }

        // Move associated files (subtitles, nfo, images)
        var destinationVideoBaseName = Path.GetFileNameWithoutExtension(destination);
        foreach (var assocFile in fileDetector.FindAssociatedFiles(source))
        {
            var assocDest = Path.Combine(destDir, destinationVideoBaseName + Path.GetExtension(assocFile));
            if (File.Exists(assocDest))
            {
                logger.LogDebug("Skipping associated file (already exists): {File}",
                    Path.GetFileName(assocFile));
                continue;
            }

            var assocOp = new MergeOperation(assocFile, assocDest, OperationType.Move, dryRun);
            operations.Add(assocOp);

            if (!dryRun)
            {
                CancellationToken.ThrowIfCancellationRequested();
                await MoveWithRetryAsync(assocFile, assocDest);
                await transactionLog.LogAsync(assocOp);
            }
        }

        return operations;
    }

    public async Task<List<MergeOperation>> RenameInPlaceAsync(string source, string destination, bool dryRun)
    {
        var operations = new List<MergeOperation>();

        var renameOp = new MergeOperation(source, destination, OperationType.Rename, dryRun);
        operations.Add(renameOp);

        if (!dryRun)
        {
            File.Move(source, destination);
            await transactionLog.LogAsync(renameOp);
        }

        // Rename associated sidecar files (subtitles, nfo, etc.) with the new base name.
        var dir = Path.GetDirectoryName(source)!;
        var newBaseName = Path.GetFileNameWithoutExtension(destination);
        foreach (var assocFile in fileDetector.FindAssociatedFiles(source))
        {
            var assocDest = Path.Combine(dir, newBaseName + Path.GetExtension(assocFile));
            if (File.Exists(assocDest)) continue;

            var assocOp = new MergeOperation(assocFile, assocDest, OperationType.Rename, dryRun);
            operations.Add(assocOp);

            if (!dryRun)
            {
                File.Move(assocFile, assocDest);
                await transactionLog.LogAsync(assocOp);
            }
        }

        return operations;
    }

    public async Task<List<MergeOperation>> DeleteFileAsync(
        string filePath,
        bool dryRun,
        OperationType operationType = OperationType.DeleteSample)
    {
        var operations = new List<MergeOperation>();

        var resolvedFilePath = ResolveAccessibleFilePath(filePath) ?? filePath;

        var filesToDelete = new List<string> { resolvedFilePath };

        filesToDelete.AddRange(fileDetector.FindAssociatedFiles(resolvedFilePath)
            .Where(f => !filesToDelete.Contains(f, StringComparer.OrdinalIgnoreCase)));

        foreach (var path in filesToDelete.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var op = new MergeOperation(path, string.Empty, operationType, dryRun);

            if (dryRun)
            {
                operations.Add(op);
            }
            else
            {
                try
                {
                    DeleteFileRobust(path);
                    operations.Add(op);
                    await transactionLog.LogAsync(op);
                }
                catch (FileNotFoundException)
                {
                    logger.LogDebug("Delete skipped (file not found): {Path}", path);
                }
                catch (DirectoryNotFoundException)
                {
                    logger.LogDebug("Delete skipped (directory not found): {Path}", path);
                }
            }
        }

        return operations;
    }

    private async Task MoveWithRetryAsync(string source, string destination)
    {
        // Use Copy + Verify + Delete instead of File.Move.
        // File.Move silently fails on Windows Storage Spaces: the API returns
        // success and File.Exists reports the move happened, but the data never
        // persists and the filesystem reverts to the pre-move state.
        var sourceLength = new FileInfo(source).Length;
        var fileName = Path.GetFileName(source);

        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await CopyWithProgressAsync(source, destination, sourceLength, fileName);

                // Verify the destination was written with the correct size.
                var destInfo = new FileInfo(destination);
                destInfo.Refresh();
                if (!destInfo.Exists || destInfo.Length != sourceLength)
                {
                    throw new IOException(
                        $"Copy verification failed: expected {sourceLength} bytes, " +
                        $"got {(destInfo.Exists ? destInfo.Length : -1)} bytes.");
                }

                // Copy verified — safe to delete the source.
                File.Delete(source);

                FileTransferProgress?.Report($"MOVED {fileName}");
                logger.LogDebug("Moved (copy+delete): {Source} → {Dest}",
                    fileName, destination);
                return;
            }
            catch (IOException ex) when (attempt < MaxRetries)
            {
                // Clean up partial copy before retrying.
                try { if (File.Exists(destination)) File.Delete(destination); }
                catch { /* best-effort cleanup */ }

                logger.LogWarning("Move failed (attempt {Attempt}/{Max}): {Error}",
                    attempt, MaxRetries, ex.Message);
                await Task.Delay(RetryDelay);
            }
        }

        // Final attempt — let it throw on failure.
        await CopyWithProgressAsync(source, destination, sourceLength, fileName);
        var finalInfo = new FileInfo(destination);
        finalInfo.Refresh();
        if (!finalInfo.Exists || finalInfo.Length != sourceLength)
        {
            throw new IOException(
                $"Final copy verification failed: expected {sourceLength} bytes, " +
                $"got {(finalInfo.Exists ? finalInfo.Length : -1)} bytes.");
        }
        File.Delete(source);
        FileTransferProgress?.Report($"MOVED {fileName}");
    }

    private async Task CopyWithProgressAsync(string source, string destination, long totalBytes, string fileName)
    {
        await using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize, useAsync: true);
        await using var destStream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, CopyBufferSize, useAsync: true);

        var buffer = new byte[CopyBufferSize];
        long bytesCopied = 0;
        int bytesRead;

        while ((bytesRead = await sourceStream.ReadAsync(buffer)) > 0)
        {
            await destStream.WriteAsync(buffer.AsMemory(0, bytesRead));
            bytesCopied += bytesRead;
            FileTransferProgress?.Report($"COPYING {fileName}|{bytesCopied}|{totalBytes}");
        }

        await destStream.FlushAsync();
    }

    private static bool FileExistsRobust(string filePath)
    {
        if (File.Exists(filePath))
            return true;

        try
        {
            return File.Exists(ToExtendedPath(filePath));
        }
        catch
        {
            return false;
        }
    }

    private static void DeleteFileRobust(string filePath)
    {
        try
        {
            File.Delete(filePath);
        }
        catch (DirectoryNotFoundException)
        {
            File.Delete(ToExtendedPath(filePath));
        }
        catch (FileNotFoundException)
        {
            File.Delete(ToExtendedPath(filePath));
        }
    }

    private static string? ResolveAccessibleFilePath(string filePath)
    {
        if (FileExistsRobust(filePath))
            return filePath;

        var parentDir = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileName(filePath);

        if (string.IsNullOrWhiteSpace(parentDir) || string.IsNullOrWhiteSpace(fileName))
            return null;

        try
        {
            var directory = new DirectoryInfo(parentDir);
            var match = directory
                .EnumerateFiles(fileName, SearchOption.TopDirectoryOnly)
                .FirstOrDefault();

            return match?.FullName;
        }
        catch
        {
            return null;
        }
    }

    private static string ToExtendedPath(string path)
    {
        if (!OperatingSystem.IsWindows())
            return path;

        if (path.StartsWith("\\\\?\\", StringComparison.Ordinal))
            return path;

        if (path.StartsWith("\\\\", StringComparison.Ordinal))
            return "\\\\?\\UNC\\" + path[2..];

        return "\\\\?\\" + path;
    }
}
