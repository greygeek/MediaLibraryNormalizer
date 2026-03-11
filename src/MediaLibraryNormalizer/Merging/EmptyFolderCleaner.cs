using MediaLibraryNormalizer.Models;
using MediaLibraryNormalizer.Scanner;
using Microsoft.Extensions.Logging;

namespace MediaLibraryNormalizer.Merging;

/// <summary>
/// Detects and removes folders containing no video files and no non-empty subfolders.
/// </summary>
public class EmptyFolderCleaner(
    IMediaFileDetector fileDetector,
    ITransactionLog transactionLog,
    ILogger<EmptyFolderCleaner> logger) : IEmptyFolderCleaner
{
    public List<string> FindEmptyFolders(string rootPath)
    {
        var emptyFolders = new List<string>();

        rootPath = ResolveAccessibleDirectoryPath(rootPath) ?? rootPath;

        if (!Directory.Exists(rootPath))
            return emptyFolders;

        IEnumerable<string> directories;
        try
        {
            directories = Directory.EnumerateDirectories(rootPath).ToList();
        }
        catch (DirectoryNotFoundException)
        {
            logger.LogDebug("Root path disappeared while checking for empty folders: {RootPath}", rootPath);
            return emptyFolders;
        }

        foreach (var dir in directories)
        {
            if (IsEmpty(dir))
                emptyFolders.Add(dir);
        }

        return emptyFolders;
    }

    public async Task<List<string>> CleanAsync(string rootPath, bool dryRun)
    {
        var result = await CleanDetailedAsync(rootPath, dryRun);
        return result.DeletedFolders;
    }

    public async Task<FolderCleanupResult> CleanDetailedAsync(string rootPath, bool dryRun)
    {
        var emptyFolders = FindEmptyFolders(rootPath);
        return await DeleteFoldersAsync(emptyFolders, dryRun);
    }

    public async Task<List<string>> CleanFoldersAsync(IEnumerable<string> folderPaths, bool dryRun)
    {
        var result = await CleanFoldersDetailedAsync(folderPaths, dryRun);
        return result.DeletedFolders;
    }

    public async Task<FolderCleanupResult> CleanFoldersDetailedAsync(IEnumerable<string> folderPaths, bool dryRun)
    {
        var emptyFolders = folderPaths
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(path => ResolveAccessibleDirectoryPath(path) ?? path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(IsEmpty)
            .ToList();

        return await DeleteFoldersAsync(emptyFolders, dryRun);
    }

    private async Task<FolderCleanupResult> DeleteFoldersAsync(List<string> emptyFolders, bool dryRun)
    {
        var result = new FolderCleanupResult();

        foreach (var folder in emptyFolders)
        {
            logger.LogInformation("{Action} empty folder: {Folder}",
                dryRun ? "Would delete" : "Deleting", folder);

            if (!dryRun)
            {
                try
                {
                    DeleteDirectoryRobust(folder);
                    await transactionLog.LogAsync(
                        new MergeOperation(folder, string.Empty, OperationType.Delete));
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete empty folder: {Folder}", folder);
                    result.Failures.Add(new FolderCleanupFailure
                    {
                        FolderPath = folder,
                        ExceptionType = ex.GetType().Name,
                        Message = ex.Message
                    });
                    continue;
                }
            }

            result.DeletedFolders.Add(folder);
        }

        return result;
    }

    private bool IsEmpty(string directoryPath)
    {
        var resolvedDirectoryPath = ResolveAccessibleDirectoryPath(directoryPath) ?? directoryPath;

        if (!Directory.Exists(resolvedDirectoryPath))
            return false;

        // Check for any video files
        if (fileDetector.EnumerateVideoFiles(resolvedDirectoryPath).Any())
            return false;

        // Recursively check subfolders
        IEnumerable<string> subDirectories;
        try
        {
            subDirectories = Directory.EnumerateDirectories(resolvedDirectoryPath).ToList();
        }
        catch (DirectoryNotFoundException)
        {
            logger.LogDebug("Directory disappeared while checking if empty: {DirectoryPath}", resolvedDirectoryPath);
            return false;
        }

        foreach (var subDir in subDirectories)
        {
            if (!IsEmpty(subDir))
                return false;
        }

        return true;
    }

    private static void DeleteDirectoryRobust(string folder)
    {
        var resolvedFolder = ResolveAccessibleDirectoryPath(folder) ?? folder;

        if (!Directory.Exists(resolvedFolder))
            return;

        ClearAttributesRecursively(resolvedFolder);

        try
        {
            Directory.Delete(resolvedFolder, recursive: true);
        }
        catch (UnauthorizedAccessException)
        {
            Directory.Delete(ToExtendedPath(resolvedFolder), recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
            Directory.Delete(ToExtendedPath(resolvedFolder), recursive: true);
        }
    }

    private static void ClearAttributesRecursively(string folder)
    {
        var pending = new Stack<string>();
        pending.Push(folder);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            foreach (var subDirectory in Directory.EnumerateDirectories(current))
            {
                pending.Push(subDirectory);
            }

            foreach (var file in Directory.EnumerateFiles(current))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            File.SetAttributes(current, FileAttributes.Directory);
        }
    }

    private static string? ResolveAccessibleDirectoryPath(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
            return directoryPath;

        var extendedDirectoryPath = ToExtendedPath(directoryPath);
        if (Directory.Exists(extendedDirectoryPath))
            return extendedDirectoryPath;

        var parentDir = Path.GetDirectoryName(directoryPath);
        var directoryName = Path.GetFileName(directoryPath);

        if (string.IsNullOrWhiteSpace(parentDir) || string.IsNullOrWhiteSpace(directoryName))
            return null;

        try
        {
            var resolvedParentDir = ResolveAccessibleDirectoryPath(parentDir) ?? parentDir;
            if (!Directory.Exists(resolvedParentDir))
                return null;

            var normalizedTargetName = directoryName.TrimEnd();
            var directory = new DirectoryInfo(resolvedParentDir);
            var match = directory
                .EnumerateDirectories("*", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(candidate =>
                    string.Equals(candidate.Name, directoryName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(candidate.Name.TrimEnd(), normalizedTargetName, StringComparison.OrdinalIgnoreCase));

            if (match is null)
                return null;

            var combinedPath = Path.Combine(resolvedParentDir, match.Name);
            if (Directory.Exists(combinedPath))
                return combinedPath;

            var extendedCombinedPath = ToExtendedPath(combinedPath);
            return Directory.Exists(extendedCombinedPath)
                ? extendedCombinedPath
                : combinedPath;
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
