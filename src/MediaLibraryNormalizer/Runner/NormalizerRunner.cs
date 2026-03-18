using MediaLibraryNormalizer.Config;
using MediaLibraryNormalizer.Matching;
using MediaLibraryNormalizer.Merging;
using MediaLibraryNormalizer.Models;
using MediaLibraryNormalizer.Normalization;
using MediaLibraryNormalizer.Parser;
using MediaLibraryNormalizer.Reporting;
using MediaLibraryNormalizer.Scanner;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MediaLibraryNormalizer.Runner;

public class NormalizerRunner : INormalizerRunner
{
    public async Task<NormalizerRunResult> RunAsync(
        NormalizerConfig config,
        IReadOnlyCollection<string>? approvedSeriesKeys = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        using var serviceProvider = ServiceRegistration.BuildServiceProvider(config);
        var logger = serviceProvider.GetRequiredService<ILogger<NormalizerRunner>>();
        var scanner = serviceProvider.GetRequiredService<ILibraryScanner>();
        var fileDetector = serviceProvider.GetRequiredService<IMediaFileDetector>();
        var normalizer = serviceProvider.GetRequiredService<INameNormalizer>();
        var episodeParser = serviceProvider.GetRequiredService<IEpisodeParser>();
        var matcher = serviceProvider.GetRequiredService<ISeriesMatcher>();
        var merger = serviceProvider.GetRequiredService<ISeriesMerger>();
        var emptyCleaner = serviceProvider.GetRequiredService<IEmptyFolderCleaner>();
        var reporter = serviceProvider.GetRequiredService<IReportGenerator>();
        var txLog = serviceProvider.GetRequiredService<ITransactionLog>();
        var fileMover = serviceProvider.GetRequiredService<IFileMover>();
        var cleanupFailures = new List<FolderCleanupFailure>();
        var approvedCleanupFolderCount = 0;
        var globalCleanupSweepFolderCount = 0;

        progress?.Report("Scanning library...");
        cancellationToken.ThrowIfCancellationRequested();

        var scanResult = new ScanResult();
        var items = scanner.Scan(config.LibraryPath).ToList();
        var topLevelVideoFileCount = CountTopLevelVideoFiles(config.LibraryPath, fileDetector);
        scanResult.AllItems = items;
        scanResult.TotalFolders = items.Count + (topLevelVideoFileCount > 0 ? 1 : 0);
        scanResult.TotalFiles = items.Sum(i => i.FileCount) + topLevelVideoFileCount;
        scanResult.UnpackFolders = items.Where(i => i.IsUnpackFolder).ToList();

        logger.LogInformation(
            "Scanned {Folders} folders, {Files} video files",
            scanResult.TotalFolders,
            scanResult.TotalFiles);

        progress?.Report("Normalizing folder names...");
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalized = item.IsUnpackFolder
                ? normalizer.NormalizeUnpackFolder(item.OriginalName)
                : normalizer.Normalize(item.OriginalName);

            item.NormalizedName = normalized.Title;
            item.Year = normalized.Year;
            item.Kind = ClassifyMediaKind(item, episodeParser);
        }

        progress?.Report("Matching duplicate series...");
        cancellationToken.ThrowIfCancellationRequested();
        scanResult.DuplicateGroups = await matcher.MatchAsync(scanResult.AllItems);

        var candidateMergeGroups = approvedSeriesKeys is null
            ? MergeGroupSelector.Select(scanResult.DuplicateGroups, config)
            : MergeGroupSelector.SelectForApprovalReview(scanResult.DuplicateGroups, config);

        var mergeGroups = approvedSeriesKeys is null
            ? MergeGroupSelector.Select(scanResult.DuplicateGroups, config)
            : MergeGroupSelector.SelectForApprovalReview(scanResult.DuplicateGroups, config, approvedSeriesKeys);

        if (config.ExactMatchesWithFilesOnly)
        {
            logger.LogInformation(
                "Selective merge mode enabled: {Selected}/{Total} duplicate groups selected (exact matches with real files only)",
            candidateMergeGroups.Count,
                scanResult.DuplicateGroups.Count);
        }

        if (approvedSeriesKeys is not null)
        {
            logger.LogInformation(
            "Approval workflow active: {Approved}/{Candidate} duplicate groups approved for execution",
            mergeGroups.Count,
            candidateMergeGroups.Count);
        }

        progress?.Report("Planning merge operations...");
        cancellationToken.ThrowIfCancellationRequested();
        var allOperations = mergeGroups.Count > 0
            ? await merger.MergeAsync(mergeGroups, config.DryRun)
            : [];

        progress?.Report("Merging similar subfolders...");
        cancellationToken.ThrowIfCancellationRequested();
        var similarFolderOperations = await merger.MergeSimilarSubfoldersAsync(scanResult.AllItems, config.DryRun);
        allOperations.AddRange(similarFolderOperations);

        progress?.Report("Flattening movie folders...");
        cancellationToken.ThrowIfCancellationRequested();
        var groupedMoviePaths = scanResult.DuplicateGroups
            .Where(static group => group.AllFolders.Any(folder => folder.Kind == MediaKind.Movie)
                                   && !group.AllFolders.Any(folder => folder.Kind == MediaKind.TvSeries))
            .SelectMany(group => group.AllFolders)
            .Select(folder => folder.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var standaloneMovies = scanResult.AllItems
            .Where(item => item.Kind == MediaKind.Movie && !groupedMoviePaths.Contains(item.Path))
            .ToList();
        var movieFlattenOperations = await merger.FlattenMovieFoldersAsync(standaloneMovies, config.DryRun);
        allOperations.AddRange(movieFlattenOperations);

        progress?.Report("Deduplicating top-level movie files...");
        cancellationToken.ThrowIfCancellationRequested();
        var topLevelMovieDedupeOperations = await merger.DeduplicateTopLevelMovieFilesAsync(config.LibraryPath, config.DryRun);
        allOperations.AddRange(topLevelMovieDedupeOperations);

        if (config.FlattenEpisodeReleaseFolders)
        {
            progress?.Report("Flattening episode release folders...");
            cancellationToken.ThrowIfCancellationRequested();
            var episodeFlattenOps = await merger.FlattenEpisodeReleaseFoldersAsync(items, config.LibraryPath, config.DryRun);
            allOperations.AddRange(episodeFlattenOps);
        }

        if (config.RenameNonStandardFiles)
        {
            progress?.Report("Renaming non-standard episode filenames...");
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var item in items.Where(static i => i.Kind == MediaKind.TvSeries))
            {
                foreach (var videoFile in item.VideoFiles)
                {
                    var newPath = episodeParser.TryNormalizeFilename(videoFile);
                    if (newPath is not null)
                    {
                        logger.LogInformation("{Action} non-standard filename: {File} \u2192 {New}",
                            config.DryRun ? "Would rename" : "Renaming",
                            Path.GetFileName(videoFile),
                            Path.GetFileName(newPath));
                        allOperations.AddRange(await fileMover.RenameInPlaceAsync(videoFile, newPath, config.DryRun));
                    }
                }
            }
        }

        if (config.DeleteSamples)
        {
            progress?.Report("Deleting sample files...");
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var item in items)
            {
                foreach (var file in Directory.EnumerateFiles(item.Path, "*.*", SearchOption.AllDirectories))
                {
                    if (fileDetector.IsSampleVideoFile(file))
                    {
                        logger.LogInformation("{Action} sample file: {File}",
                            config.DryRun ? "Would delete" : "Deleting", Path.GetFileName(file));
                        allOperations.AddRange(await fileMover.DeleteFileAsync(file, config.DryRun, OperationType.DeleteSample));
                    }
                }
            }
        }

        if (config.DeleteNonEpisodeFiles)
        {
            progress?.Report("Deleting non-episode video files...");
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var item in items.Where(static i => i.Kind == MediaKind.TvSeries))
            {
                foreach (var videoFile in item.VideoFiles)
                {
                    if (episodeParser.Parse(videoFile) is null)
                    {
                        logger.LogInformation("{Action} non-episode file: {File}",
                            config.DryRun ? "Would delete" : "Deleting", Path.GetFileName(videoFile));
                        allOperations.AddRange(await fileMover.DeleteFileAsync(videoFile, config.DryRun, OperationType.DeleteNonEpisode));
                    }
                }
            }
        }

        if (approvedSeriesKeys is not null && mergeGroups.Count > 0)
        {
            progress?.Report("Cleaning approved duplicate folders...");
            cancellationToken.ThrowIfCancellationRequested();

            var cleanupResult = await emptyCleaner.CleanFoldersDetailedAsync(
                mergeGroups.SelectMany(group => group.DuplicateFolders)
                    .Select(folder => folder.Path),
                config.DryRun);

            cleanupFailures.AddRange(cleanupResult.Failures);
            scanResult.Errors.AddRange(cleanupResult.Failures.Select(static failure =>
                $"Cleanup failed: {failure.FolderPath} ({failure.ExceptionType}) {failure.Message}"));
            approvedCleanupFolderCount = cleanupResult.DeletedFolders.Count;

            allOperations.AddRange(cleanupResult.DeletedFolders.Select(folder =>
                new MergeOperation(folder, string.Empty, OperationType.Delete, config.DryRun)));

            progress?.Report("Running global empty-folder cleanup sweep...");
            cancellationToken.ThrowIfCancellationRequested();

            var sweepResult = await emptyCleaner.CleanDetailedAsync(config.LibraryPath, config.DryRun);
            scanResult.EmptyFolders = sweepResult.DeletedFolders;
            cleanupFailures.AddRange(sweepResult.Failures);
            scanResult.Errors.AddRange(sweepResult.Failures.Select(static failure =>
                $"Cleanup failed: {failure.FolderPath} ({failure.ExceptionType}) {failure.Message}"));
            globalCleanupSweepFolderCount = sweepResult.DeletedFolders.Count;

            allOperations.AddRange(sweepResult.DeletedFolders
                .Except(cleanupResult.DeletedFolders, StringComparer.OrdinalIgnoreCase)
                .Select(folder => new MergeOperation(folder, string.Empty, OperationType.Delete, config.DryRun)));
        }

        if (config.ExactMatchesWithFilesOnly && approvedSeriesKeys is null)
        {
            logger.LogInformation(
                "Skipping global empty-folder cleanup because selective merge mode is enabled");
        }
        else if (approvedSeriesKeys is null)
        {
            progress?.Report("Cleaning empty folders...");
            cancellationToken.ThrowIfCancellationRequested();
            var cleanupResult = await emptyCleaner.CleanDetailedAsync(config.LibraryPath, config.DryRun);
            scanResult.EmptyFolders = cleanupResult.DeletedFolders;
            cleanupFailures.AddRange(cleanupResult.Failures);
            scanResult.Errors.AddRange(cleanupResult.Failures.Select(static failure =>
                $"Cleanup failed: {failure.FolderPath} ({failure.ExceptionType}) {failure.Message}"));
            globalCleanupSweepFolderCount = cleanupResult.DeletedFolders.Count;

            allOperations.AddRange(cleanupResult.DeletedFolders.Select(folder =>
                new MergeOperation(folder, string.Empty, OperationType.Delete, config.DryRun)));
        }

        progress?.Report("Writing report...");
        var reportPath = Path.Combine(config.LibraryPath, "media-clean-report.json");
        await reporter.WriteJsonReportAsync(reportPath, scanResult, allOperations, config.DryRun);
        logger.LogInformation("JSON report saved: {Path}", reportPath);

        string? transactionLogPath = null;
        if (!config.DryRun)
        {
            transactionLogPath = Path.Combine(config.LibraryPath, "media-clean-transactions.json");
            await txLog.SaveAsync(transactionLogPath);
        }

        progress?.Report(config.DryRun ? "Dry run completed." : "Live run completed.");

        return new NormalizerRunResult
        {
            Config = CloneConfig(config),
            ScanResult = scanResult,
            Operations = allOperations,
            SelectedDuplicateGroups = mergeGroups.Count,
            SelectedSeriesKeys = mergeGroups.Select(group => group.SeriesKey).ToList(),
            CleanupFailures = cleanupFailures,
            ApprovedCleanupFolderCount = approvedCleanupFolderCount,
            GlobalCleanupSweepFolderCount = globalCleanupSweepFolderCount,
            ReportPath = reportPath,
            TransactionLogPath = transactionLogPath
        };
    }

    public async Task UndoAsync(
        NormalizerConfig config,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (string.IsNullOrWhiteSpace(config.UndoFile))
        {
            throw new InvalidOperationException("Undo file must be provided.");
        }

        using var serviceProvider = ServiceRegistration.BuildServiceProvider(config);
        var undoService = serviceProvider.GetRequiredService<UndoService>();

        progress?.Report("Undoing operations...");
        cancellationToken.ThrowIfCancellationRequested();
        await undoService.UndoAsync(config.UndoFile);
        progress?.Report("Undo completed.");
    }

    private static NormalizerConfig CloneConfig(NormalizerConfig config)
    {
        return new NormalizerConfig
        {
            LibraryPath = config.LibraryPath,
            FuzzyThreshold = config.FuzzyThreshold,
            AiThreshold = config.AiThreshold,
            HashSizeMB = config.HashSizeMB,
            MaxConcurrency = config.MaxConcurrency,
            DeleteSamples = config.DeleteSamples,
            DeleteNonEpisodeFiles = config.DeleteNonEpisodeFiles,
            RenameNonStandardFiles = config.RenameNonStandardFiles,
            FlattenEpisodeReleaseFolders = config.FlattenEpisodeReleaseFolders,
            DryRun = config.DryRun,
            Merge = config.Merge,
            ExactMatchesWithFilesOnly = config.ExactMatchesWithFilesOnly,
            UseAi = config.UseAi,
            DiscardInferiorDuplicates = config.DiscardInferiorDuplicates,
            UseHash = config.UseHash,
            Verbose = config.Verbose,
            UndoFile = config.UndoFile,
            AiEndpoint = config.AiEndpoint,
            AiApiKey = config.AiApiKey,
            AiModel = config.AiModel
        };
    }

    private static MediaKind ClassifyMediaKind(MediaItem item, IEpisodeParser episodeParser)
    {
        if (item.SeasonFolders.Count > 0)
            return MediaKind.TvSeries;

        if (item.VideoFiles.Any(videoFile => episodeParser.Parse(videoFile) is not null))
            return MediaKind.TvSeries;

        return item.VideoFiles.Count > 0
            ? MediaKind.Movie
            : MediaKind.Unknown;
    }

    private static int CountTopLevelVideoFiles(string libraryPath, IMediaFileDetector fileDetector)
    {
        if (!Directory.Exists(libraryPath))
            return 0;

        return Directory.EnumerateFiles(libraryPath, "*.*", SearchOption.TopDirectoryOnly)
            .Count(fileDetector.IsVideoFile);
    }
}
