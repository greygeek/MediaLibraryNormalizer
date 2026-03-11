using MediaLibraryNormalizer.Matching;
using MediaLibraryNormalizer.Config;
using MediaLibraryNormalizer.Models;
using MediaLibraryNormalizer.Parser;
using MediaLibraryNormalizer.Scanner;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace MediaLibraryNormalizer.Merging;

/// <summary>
/// Merges duplicate series folders into canonical folders.
/// Preserves season directory structure and resolves duplicate episodes by quality.
/// </summary>
public class SeriesMerger(
    IFileMover fileMover,
    IMediaFileDetector fileDetector,
    ITransactionLog transactionLog,
    IEpisodeParser episodeParser,
    IDuplicateDetector duplicateDetector,
    NormalizerConfig config,
    ILogger<SeriesMerger> logger) : ISeriesMerger
{
    private static readonly Regex SeasonFolderRegex = new(
        @"^(season|series)\s+(\d+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ResolutionRegex = new(
        @"\b(2160p|1080p|720p|576p|480p|4k)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CodecRegex = new(
        @"\b(x264|x265|h\.?264|h\.?265|hevc|avc|xvid|divx|vp9|av1)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<List<MergeOperation>> MergeAsync(List<SeriesGroup> groups, bool dryRun)
    {
        var allOperations = new List<MergeOperation>();

        foreach (var group in groups)
        {
            if (group.CanonicalFolder is null)
                continue;

            var ops = GetGroupMediaKind(group) == MediaKind.Movie
                ? await MergeMovieGroupAsync(group, dryRun)
                : await MergeTelevisionGroupAsync(group, dryRun);

            allOperations.AddRange(ops);
            group.PlannedOperations.AddRange(ops);
        }

        logger.LogInformation("Total merge operations: {Count} (dryRun={DryRun})",
            allOperations.Count, dryRun);

        return allOperations;
    }

    public async Task<List<MergeOperation>> FlattenMovieFoldersAsync(IEnumerable<MediaItem> items, bool dryRun)
    {
        var allOperations = new List<MergeOperation>();

        foreach (var item in items.Where(static item => item.Kind == MediaKind.Movie))
        {
            var operations = await FlattenMovieFolderAsync(item, dryRun);
            allOperations.AddRange(operations);
        }

        if (allOperations.Count > 0)
        {
            logger.LogInformation(
                "Total movie folder flatten operations: {Count} (dryRun={DryRun})",
                allOperations.Count,
                dryRun);
        }

        return allOperations;
    }

    public async Task<List<MergeOperation>> MergeSimilarSubfoldersAsync(IEnumerable<MediaItem> items, bool dryRun)
    {
        var allOperations = new List<MergeOperation>();

        foreach (var item in items.Where(static item => item.Kind != MediaKind.Movie))
        {
            var operations = await MergeEquivalentSeasonFoldersAsync(item, dryRun);
            allOperations.AddRange(operations);
        }

        if (allOperations.Count > 0)
        {
            logger.LogInformation(
                "Total similar-subfolder merge operations: {Count} (dryRun={DryRun})",
                allOperations.Count,
                dryRun);
        }

        return allOperations;
    }

    private async Task<List<MergeOperation>> MergeTelevisionGroupAsync(SeriesGroup group, bool dryRun)
    {
        var allOperations = new List<MergeOperation>();

        foreach (var dupeFolder in group.DuplicateFolders)
        {
            var ops = await MergeFolderAsync(dupeFolder, group.CanonicalFolder!, dryRun);
            allOperations.AddRange(ops);
        }

        return allOperations;
    }

    private async Task<List<MergeOperation>> MergeMovieGroupAsync(SeriesGroup group, bool dryRun)
    {
        var operations = new List<MergeOperation>();
        var libraryRoot = GetLibraryRoot(group.CanonicalFolder!);
        var candidates = group.AllFolders
            .SelectMany(folder => folder.VideoFiles)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(BuildMovieCandidate)
            .ToList();

        if (candidates.Count == 0)
            return operations;

        var winner = candidates
            .OrderByDescending(static candidate => candidate.ResolutionValue)
            .ThenByDescending(static candidate => candidate.CodecRank)
            .ThenByDescending(static candidate => candidate.FileSize)
            .ThenByDescending(static candidate => candidate.ModifiedDate)
            .First();

        var winnerDestination = Path.Combine(libraryRoot, Path.GetFileName(winner.Path));

        logger.LogInformation(
            "Flattening duplicate movie '{Movie}' to '{DestinationFile}'",
            group.CanonicalName,
            Path.GetFileName(winnerDestination));

        if (!string.Equals(winner.Path, winnerDestination, StringComparison.OrdinalIgnoreCase)
            && !File.Exists(winnerDestination))
        {
            operations.AddRange(await fileMover.MoveFileAsync(winner.Path, winnerDestination, dryRun));
        }

        foreach (var candidate in candidates.Where(candidate => !string.Equals(candidate.Path, winner.Path, StringComparison.OrdinalIgnoreCase)))
        {
            logger.LogInformation("{Action} duplicate movie file: {File}",
                dryRun ? "Would delete" : "Deleting",
                Path.GetFileName(candidate.Path));

            operations.AddRange(await fileMover.DeleteFileAsync(candidate.Path, dryRun, OperationType.DeleteDuplicate));
        }

        foreach (var folder in group.AllFolders)
        {
            if (CanDeleteMergedFolder(folder.Path, operations, dryRun))
            {
                var deleteOp = new MergeOperation(folder.Path, string.Empty, OperationType.Delete, dryRun);
                operations.Add(deleteOp);

                if (!dryRun)
                {
                    DeleteDirectoryRobust(folder.Path);
                    await transactionLog.LogAsync(deleteOp);
                }
            }
        }

        return operations;
    }

    private async Task<List<MergeOperation>> FlattenMovieFolderAsync(MediaItem item, bool dryRun)
    {
        var operations = new List<MergeOperation>();
        var libraryRoot = GetLibraryRoot(item);

        foreach (var videoFile in item.VideoFiles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var destination = Path.Combine(libraryRoot, Path.GetFileName(videoFile));
            if (string.Equals(videoFile, destination, StringComparison.OrdinalIgnoreCase)
                || File.Exists(destination))
            {
                continue;
            }

            operations.AddRange(await fileMover.MoveFileAsync(videoFile, destination, dryRun));
        }

        if (CanDeleteMergedFolder(item.Path, operations, dryRun))
        {
            var deleteOp = new MergeOperation(item.Path, string.Empty, OperationType.Delete, dryRun);
            operations.Add(deleteOp);

            if (!dryRun)
            {
                DeleteDirectoryRobust(item.Path);
                await transactionLog.LogAsync(deleteOp);
            }
        }

        return operations;
    }

    private async Task<List<MergeOperation>> MergeFolderAsync(
        MediaItem source, MediaItem canonical, bool dryRun)
    {
        var operations = new List<MergeOperation>();

        logger.LogInformation("Merging '{Source}' → '{Canonical}'",
            source.OriginalName, canonical.OriginalName);

        // Build episode index of canonical folder to detect duplicates
        var canonicalEpisodes = canonical.VideoFiles
            .Select(f => episodeParser.Parse(f))
            .Where(e => e is not null)
            .Cast<EpisodeInfo>()
            .ToList();

        foreach (var videoFile in source.VideoFiles)
        {
            var sourceEpisode = episodeParser.Parse(videoFile);

            // Determine destination path, preserving season structure
            var relativePath = Path.GetRelativePath(source.Path, videoFile);
            var destPath = BuildDestinationPath(canonical, relativePath, sourceEpisode);

            // Check for duplicate
            if (sourceEpisode is not null)
            {
                var duplicate = FindDuplicate(sourceEpisode, canonicalEpisodes);
                if (duplicate is not null)
                {
                    // Run quality comparison
                    var dupResult = duplicateDetector.DetectDuplicates([sourceEpisode, duplicate]);

                    if (dupResult.Discard.Any(d => d.FilePath == videoFile))
                    {
                        if (config.DiscardInferiorDuplicates)
                        {
                            var resolvedSourcePath = ResolveAccessiblePath(videoFile, source.Path);

                            logger.LogInformation("{Action} inferior duplicate: {File}",
                                dryRun ? "Would delete" : "Deleting",
                                Path.GetFileName(videoFile));

                            var deleteOps = await fileMover.DeleteFileAsync(
                                resolvedSourcePath,
                                dryRun,
                                OperationType.DeleteDuplicate);
                            operations.AddRange(deleteOps);
                        }
                        else
                        {
                            logger.LogDebug("Skipping inferior duplicate: {File}",
                                Path.GetFileName(videoFile));
                        }

                        continue;
                    }

                    // Source is better — the existing file in canonical will remain
                    // but we move the better file over (if not same path)
                    if (dupResult.Discard.Any(d => d.FilePath == duplicate.FilePath))
                    {
                        logger.LogDebug("Source file is better quality: {File}",
                            Path.GetFileName(videoFile));

                        if (config.DiscardInferiorDuplicates)
                        {
                            var resolvedCanonicalDuplicatePath = ResolveAccessiblePath(
                                duplicate.FilePath,
                                canonical.Path);

                            logger.LogInformation("{Action} inferior canonical duplicate: {File}",
                                dryRun ? "Would delete" : "Deleting",
                                Path.GetFileName(duplicate.FilePath));

                            var deleteOps = await fileMover.DeleteFileAsync(
                                resolvedCanonicalDuplicatePath,
                                dryRun,
                                OperationType.DeleteDuplicate);
                            operations.AddRange(deleteOps);
                            canonicalEpisodes.Remove(duplicate);
                        }
                    }
                }
            }

            // Skip if destination already exists and we don't have a quality winner
            if (File.Exists(destPath))
            {
                logger.LogDebug("Destination exists, skipping: {File}", Path.GetFileName(videoFile));
                continue;
            }

            var moveOps = await fileMover.MoveFileAsync(videoFile, destPath, dryRun);
            operations.AddRange(moveOps);

            if (sourceEpisode is not null)
            {
                canonicalEpisodes.Add(CloneForDestination(sourceEpisode, destPath));
            }
        }

        return operations;
    }

    private static string BuildDestinationPath(
        MediaItem canonical,
        string relativePath,
        EpisodeInfo? sourceEpisode)
    {
        var segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0)
            return Path.Combine(canonical.Path, relativePath);

        if (TryGetSeasonFolderNumber(segments[0], out var seasonNumber))
        {
            segments[0] = ResolveCanonicalSeasonFolderName(canonical, seasonNumber);
        }
        else if (segments.Length == 1
            && sourceEpisode is not null
            && !TryGetSeasonFolderNumber(Path.GetFileName(canonical.Path), out _))
        {
            segments = [$"Season {sourceEpisode.Season:D2}", segments[0]];
        }

        return Path.Combine(canonical.Path, Path.Combine(segments));
    }

    private async Task<List<MergeOperation>> MergeEquivalentSeasonFoldersAsync(MediaItem item, bool dryRun)
    {
        var operations = new List<MergeOperation>();
        var seasonFolders = DiscoverSeasonFolders(item).ToList();

        var seasonGroups = seasonFolders
            .Select(path => new { Path = path, Name = Path.GetFileName(path) })
            .Where(x => TryGetSeasonFolderNumber(x.Name, out _))
            .GroupBy(x => GetSeasonFolderKey(x.Name), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1);

        foreach (var seasonGroup in seasonGroups)
        {
            var folderPaths = seasonGroup.Select(x => x.Path).ToList();
            var targetPath = ResolvePreferredSeasonFolderPath(item.Path, folderPaths, seasonGroup.Key);

            foreach (var sourcePath in folderPaths.Where(path => !string.Equals(path, targetPath, StringComparison.OrdinalIgnoreCase)))
            {
                var sourceItem = BuildFolderItem(sourcePath);
                if (sourceItem.VideoFiles.Count == 0)
                    continue;

                var targetItem = BuildFolderItem(targetPath);

                logger.LogInformation(
                    "Merging similar folder '{Source}' → '{Canonical}' in '{Series}'",
                    Path.GetFileName(sourcePath),
                    Path.GetFileName(targetPath),
                    item.OriginalName);

                var mergeOps = await MergeFolderAsync(sourceItem, targetItem, dryRun);
                operations.AddRange(mergeOps);

                if (CanDeleteMergedFolder(sourcePath, mergeOps, dryRun))
                {
                    var deleteOp = new MergeOperation(sourcePath, string.Empty, OperationType.Delete, dryRun);
                    operations.Add(deleteOp);

                    if (!dryRun)
                    {
                        DeleteDirectoryRobust(sourcePath);
                        await transactionLog.LogAsync(deleteOp);
                    }
                }
            }
        }

        return operations;
    }

    private static string ResolveCanonicalSeasonFolderName(MediaItem canonical, int seasonNumber)
    {
        var candidateFolderNames = canonical.SeasonFolders
            .Select(Path.GetFileName)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>();

        var preferredName = $"Season {seasonNumber:D2}";

        foreach (var folderName in candidateFolderNames)
        {
            if (string.Equals(folderName, preferredName, StringComparison.OrdinalIgnoreCase))
                return folderName;
        }

        foreach (var folderName in candidateFolderNames)
        {
            if (TryGetSeasonFolderNumber(folderName, out var folderSeason)
                && folderSeason == seasonNumber)
            {
                return folderName;
            }
        }

        return preferredName;
    }

    private static string GetSeasonFolderKey(string folderName)
    {
        return TryGetSeasonFolderNumber(folderName, out var seasonNumber)
            ? seasonNumber.ToString("D4")
            : folderName;
    }

    private static IEnumerable<string> DiscoverSeasonFolders(MediaItem item)
    {
        var knownFolders = item.SeasonFolders;
        if (knownFolders.Count > 0)
            return knownFolders;

        if (!Directory.Exists(item.Path))
            return [];

        return Directory.EnumerateDirectories(item.Path);
    }

    private MediaItem BuildFolderItem(string folderPath)
    {
        var videoFiles = fileDetector.EnumerateVideoFiles(folderPath).ToList();
        var seasonFolders = Directory.Exists(folderPath)
            ? Directory.EnumerateDirectories(folderPath).ToList()
            : [];

        return new MediaItem
        {
            Path = folderPath,
            OriginalName = Path.GetFileName(folderPath),
            NormalizedName = Path.GetFileName(folderPath),
            FileCount = videoFiles.Count,
            VideoFiles = videoFiles,
            SeasonFolders = seasonFolders,
            Kind = seasonFolders.Count > 0 ? MediaKind.TvSeries : MediaKind.Unknown
        };
    }

    private static string GetLibraryRoot(MediaItem item)
    {
        return Path.GetDirectoryName(item.Path) ?? item.Path;
    }

    private MediaKind GetGroupMediaKind(SeriesGroup group)
    {
        return group.AllFolders.Any(IsTelevisionLike)
            ? MediaKind.TvSeries
            : MediaKind.Movie;
    }

    private bool IsTelevisionLike(MediaItem item)
    {
        if (item.Kind == MediaKind.TvSeries)
            return true;

        if (item.Kind == MediaKind.Movie)
            return false;

        if (item.SeasonFolders.Count > 0)
            return true;

        return item.VideoFiles.Any(videoFile => episodeParser.Parse(videoFile) is not null);
    }

    private static MovieCandidate BuildMovieCandidate(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        long fileSize = 0;
        DateTime modifiedDate = DateTime.MinValue;

        try
        {
            if (File.Exists(filePath))
            {
                var fileInfo = new FileInfo(filePath);
                fileSize = fileInfo.Length;
                modifiedDate = fileInfo.LastWriteTimeUtc;
            }
        }
        catch
        {
            // Ignore metadata read failures and keep default ranking values.
        }

        return new MovieCandidate(
            filePath,
            ParseResolutionValue(fileName),
            GetCodecRank(fileName),
            fileSize,
            modifiedDate);
    }

    private static int ParseResolutionValue(string fileName)
    {
        var match = ResolutionRegex.Match(fileName);
        if (!match.Success)
            return 0;

        return match.Groups[1].Value.ToLowerInvariant() switch
        {
            "4k" or "2160p" => 2160,
            "1080p" => 1080,
            "720p" => 720,
            "576p" => 576,
            "480p" => 480,
            _ => 0
        };
    }

    private static int GetCodecRank(string fileName)
    {
        var match = CodecRegex.Match(fileName);
        if (!match.Success)
            return 0;

        return match.Groups[1].Value.ToLowerInvariant().Replace(".", string.Empty) switch
        {
            "av1" => 5,
            "x265" or "h265" or "hevc" => 4,
            "x264" or "h264" or "avc" => 3,
            "vp9" => 2,
            "xvid" or "divx" => 1,
            _ => 0
        };
    }

    private sealed record MovieCandidate(
        string Path,
        int ResolutionValue,
        int CodecRank,
        long FileSize,
        DateTime ModifiedDate);

    private static string ResolvePreferredSeasonFolderPath(string rootPath, List<string> folderPaths, string seasonGroupKey)
    {
        var seasonNumber = int.Parse(seasonGroupKey);
        var preferredName = $"Season {seasonNumber:D2}";
        var existingPreferred = folderPaths.FirstOrDefault(path =>
            string.Equals(Path.GetFileName(path), preferredName, StringComparison.OrdinalIgnoreCase));

        return existingPreferred ?? Path.Combine(rootPath, preferredName);
    }

    private bool CanDeleteMergedFolder(string folderPath, List<MergeOperation> mergeOps, bool dryRun)
    {
        var resolvedPath = ResolveAccessibleDirectoryPath(folderPath) ?? folderPath;
        if (!Directory.Exists(resolvedPath))
            return false;

        if (dryRun)
        {
            var files = Directory.EnumerateFiles(resolvedPath, "*.*", SearchOption.AllDirectories).ToList();
            if (files.Count == 0)
                return true;

            var handledFiles = mergeOps
                .Where(op => op.Type is OperationType.Move or OperationType.DeleteDuplicate or OperationType.DeleteSample)
                .Select(op => op.Source)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return files.All(handledFiles.Contains);
        }

        return !fileDetector.EnumerateVideoFiles(resolvedPath).Any()
            && !Directory.EnumerateFiles(resolvedPath, "*.*", SearchOption.AllDirectories).Any();
    }

    private static void DeleteDirectoryRobust(string folderPath)
    {
        var resolvedPath = ResolveAccessibleDirectoryPath(folderPath) ?? folderPath;
        if (!Directory.Exists(resolvedPath))
            return;

        try
        {
            Directory.Delete(resolvedPath, recursive: true);
        }
        catch (UnauthorizedAccessException)
        {
            Directory.Delete(ToExtendedPath(resolvedPath), recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
            Directory.Delete(ToExtendedPath(resolvedPath), recursive: true);
        }
    }

    private static string? ResolveAccessibleDirectoryPath(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
            return directoryPath;

        var extendedDirectoryPath = ToExtendedPath(directoryPath);
        return Directory.Exists(extendedDirectoryPath)
            ? extendedDirectoryPath
            : null;
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

    private static bool TryGetSeasonFolderNumber(string folderName, out int seasonNumber)
    {
        seasonNumber = default;

        if (string.Equals(folderName, "Specials", StringComparison.OrdinalIgnoreCase))
        {
            seasonNumber = 0;
            return true;
        }

        var match = SeasonFolderRegex.Match(folderName);
        if (!match.Success)
            return false;

        seasonNumber = int.Parse(match.Groups[2].Value);
        return true;
    }

    private static EpisodeInfo? FindDuplicate(EpisodeInfo source, List<EpisodeInfo> canonicalEpisodes)
    {
        return canonicalEpisodes.FirstOrDefault(ce =>
            ce.Season == source.Season &&
            ce.Episodes.Any(e => source.Episodes.Contains(e)));
    }

    private static EpisodeInfo CloneForDestination(EpisodeInfo source, string destinationPath)
    {
        return new EpisodeInfo
        {
            FilePath = destinationPath,
            SeriesName = source.SeriesName,
            Season = source.Season,
            Episodes = [.. source.Episodes],
            Year = source.Year,
            Resolution = source.Resolution,
            ResolutionValue = source.ResolutionValue,
            Codec = source.Codec,
            Source = source.Source,
            FileSize = source.FileSize,
            ModifiedDate = source.ModifiedDate
        };
    }

    private static string ResolveAccessiblePath(string filePath, string folderRoot)
    {
        if (File.Exists(filePath))
            return filePath;

        try
        {
            var relativePath = filePath.StartsWith(folderRoot, StringComparison.OrdinalIgnoreCase)
                ? filePath[folderRoot.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : Path.GetRelativePath(folderRoot, filePath);

            var segments = relativePath.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length == 0)
                return filePath;

            var current = new DirectoryInfo(folderRoot);

            for (var i = 0; i < segments.Length - 1; i++)
            {
                current = current
                    .EnumerateDirectories(segments[i], SearchOption.TopDirectoryOnly)
                    .FirstOrDefault() ?? throw new DirectoryNotFoundException();
            }

            var file = current
                .EnumerateFiles(segments[^1], SearchOption.TopDirectoryOnly)
                .FirstOrDefault();

            return file?.FullName ?? filePath;
        }
        catch
        {
            return filePath;
        }
    }
}
