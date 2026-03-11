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
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);

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

        // Move the video file
        var moveOp = new MergeOperation(source, destination, OperationType.Move, dryRun);
        operations.Add(moveOp);

        if (!dryRun)
        {
            await MoveWithRetryAsync(source, destination);
            await transactionLog.LogAsync(moveOp);
        }

        // Move associated files (subtitles, nfo, images)
        foreach (var assocFile in fileDetector.FindAssociatedFiles(source))
        {
            var assocDest = Path.Combine(destDir, Path.GetFileName(assocFile));
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
                await MoveWithRetryAsync(assocFile, assocDest);
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
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                File.Move(source, destination);
                logger.LogDebug("Moved: {Source} → {Dest}",
                    Path.GetFileName(source), destination);
                return;
            }
            catch (IOException ex) when (attempt < MaxRetries)
            {
                logger.LogWarning("Move failed (attempt {Attempt}/{Max}): {Error}",
                    attempt, MaxRetries, ex.Message);
                await Task.Delay(RetryDelay);
            }
        }

        // Final attempt — let it throw
        File.Move(source, destination);
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
