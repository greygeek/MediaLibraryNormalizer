using MediaLibraryNormalizer.AI;
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
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, string>? catalogSourceIds = null)
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

        // Wire per-file transfer progress so the UI can show copy progress.
        fileMover.FileTransferProgress = progress is not null
            ? new Progress<string>(msg => progress.Report(msg))
            : null;
        fileMover.CancellationToken = cancellationToken;

        List<MediaItem> ScanAndNormalizeItems()
        {
            var scannedItems = scanner.Scan(config.LibraryPath).ToList();

            foreach (var item in scannedItems)
            {
                var normalized = item.IsUnpackFolder
                    ? normalizer.NormalizeUnpackFolder(item.OriginalName)
                    : normalizer.Normalize(item.OriginalName);

                item.NormalizedName = normalized.Title;
                item.Year = normalized.Year;
                item.Kind = ClassifyMediaKind(item, episodeParser);
            }

            return scannedItems;
        }

        progress?.Report("Scanning library...");
        cancellationToken.ThrowIfCancellationRequested();

        var scanResult = new ScanResult();
        var items = ScanAndNormalizeItems();
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
        cancellationToken.ThrowIfCancellationRequested();

        progress?.Report("Matching duplicate series...");
        cancellationToken.ThrowIfCancellationRequested();

        // Enrich items with catalog source IDs from audit data when available.
        // The dictionary keys are original folder names (not full paths) for reliable
        // cross-run matching regardless of path normalisation differences.
        if (catalogSourceIds is { Count: > 0 })
        {
            var enriched = 0;
            foreach (var item in scanResult.AllItems)
            {
                if (catalogSourceIds.TryGetValue(item.OriginalName, out var sourceId))
                {
                    item.CatalogSourceId = sourceId;
                    enriched++;
                }
            }

            progress?.Report($"Catalog enrichment: {enriched}/{scanResult.AllItems.Count} folders matched from {catalogSourceIds.Count} audit entries");
            logger.LogInformation(
                "Catalog enrichment: {Enriched}/{Total} items matched from {Available} audit entries",
                enriched, scanResult.AllItems.Count, catalogSourceIds.Count);
        }
        else
        {
            progress?.Report("No catalog source IDs available — run Missing Episode Finder first");
            logger.LogInformation("No catalog source IDs available — catalog-based duplicate detection skipped");
        }

        scanResult.DuplicateGroups = await matcher.MatchAsync(scanResult.AllItems);

        var candidateMergeGroups = approvedSeriesKeys is null
            ? MergeGroupSelector.Select(scanResult.DuplicateGroups, config)
            : MergeGroupSelector.SelectForApprovalReview(scanResult.DuplicateGroups, config);

        var mergeGroups = approvedSeriesKeys is null
            ? MergeGroupSelector.Select(scanResult.DuplicateGroups, config)
            : MergeGroupSelector.SelectForApprovalReview(scanResult.DuplicateGroups, config, approvedSeriesKeys);

        progress?.Report($"Duplicate detection: {scanResult.DuplicateGroups.Count} groups found, {candidateMergeGroups.Count} candidates, {mergeGroups.Count} approved for merge");

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

        // Write diagnostic to file since Avalonia WinExe has no console
        var diagPath = Path.Combine(config.LibraryPath, "merge-diagnostic.log");
        void Diag(string msg)
        {
            var line = $"{DateTime.UtcNow:o}  {msg}";
            File.AppendAllText(diagPath, line + Environment.NewLine);
            progress?.Report(msg);
        }

        File.WriteAllText(diagPath, string.Empty); // truncate

        Diag($"config.DryRun={config.DryRun}");
        Diag($"approvedSeriesKeys={(approvedSeriesKeys is null ? "<null>" : $"[{string.Join(", ", approvedSeriesKeys)}]")}");
        Diag($"scanResult.DuplicateGroups.Count={scanResult.DuplicateGroups.Count}");
        foreach (var dg in scanResult.DuplicateGroups)
            Diag($"  group: key='{dg.SeriesKey}' method={dg.MatchMethod} folders={dg.AllFolders.Count}");
        Diag($"candidateMergeGroups.Count={candidateMergeGroups.Count}");
        Diag($"mergeGroups.Count={mergeGroups.Count}");
        foreach (var mg in mergeGroups)
            Diag($"  approved group: key='{mg.SeriesKey}' folders={mg.AllFolders.Count} dupes={mg.DuplicateFolders.Count()} files={mg.DuplicateFolders.Sum(f => f.VideoFiles.Count)}");

        cancellationToken.ThrowIfCancellationRequested();
        var allOperations = mergeGroups.Count > 0
            ? await merger.MergeAsync(mergeGroups, config.DryRun)
            : [];

        Diag($"MergeAsync returned {allOperations.Count} operations (dryRun={config.DryRun})");

        // When running in approval mode, only the approved duplicate-group merges
        // and their folder cleanup should execute. The library-wide phases below
        // (subfolder merging, flattening, renaming, deleting) are not part of the
        // approval scope and must not touch unapproved series.
        var isApprovalRun = approvedSeriesKeys is not null;

        if (!isApprovalRun)
        {
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
            progress?.Report("Moving top-level episode files...");
            cancellationToken.ThrowIfCancellationRequested();
            items = ScanAndNormalizeItems();
            var topLevelEpisodeOps = await merger.FlattenTopLevelEpisodeFilesAsync(config.LibraryPath, items, config.DryRun);
            allOperations.AddRange(topLevelEpisodeOps);

            progress?.Report("Flattening episode release folders...");
            cancellationToken.ThrowIfCancellationRequested();
            items = ScanAndNormalizeItems();
            var episodeFlattenOps = await merger.FlattenEpisodeReleaseFoldersAsync(items, config.LibraryPath, config.DryRun);
            allOperations.AddRange(episodeFlattenOps);

            progress?.Report("Flattening orphaned series folders...");
            cancellationToken.ThrowIfCancellationRequested();
            items = ScanAndNormalizeItems();
            var orphanFlattenOps = await merger.FlattenOrphanedSeriesFoldersAsync(items, config.LibraryPath, config.DryRun);
            allOperations.AddRange(orphanFlattenOps);

            // The flatten phases may have created new Season N subfolders inside an existing
            // series folder that already had Season 0N subfolders (e.g. "Season 1" alongside
            // "Season 01"). Run MergeSimilarSubfoldersAsync again with a fresh scan so those
            // pairs are merged and the canonical zero-padded name is preserved.
            progress?.Report("Merging season sub-folders created by flatten...");
            cancellationToken.ThrowIfCancellationRequested();
            items = ScanAndNormalizeItems();
            var postFlattenSubfolderOps = await merger.MergeSimilarSubfoldersAsync(items, config.DryRun);
            allOperations.AddRange(postFlattenSubfolderOps);
        }

        if (config.UseAiOrganizer)
        {
            progress?.Report("AI-assisted file organization...");
            cancellationToken.ThrowIfCancellationRequested();
            items = ScanAndNormalizeItems();
            var aiOrganizer = serviceProvider.GetRequiredService<IAiOrganizer>();

            // Collect video files in small flat folders whose paths have no parseable episode token.
            // Items already handled by flatten (folder name has episode token) will have been moved,
            // so Directory.Exists guards against double-processing.
            var nonConformingFiles = items
                .Where(item =>
                    item.SeasonFolders.Count == 0 &&
                    item.VideoFiles.Count is >= 1 and <= 3 &&
                    episodeParser.Parse(item.OriginalName) is null &&
                    item.VideoFiles.All(vf => episodeParser.Parse(vf) is null) &&
                    Directory.Exists(item.Path))
                .SelectMany(item => item.VideoFiles)
                .ToList();

            if (nonConformingFiles.Count > 0)
            {
                logger.LogInformation("AI organizer: sending {Count} non-conforming video files", nonConformingFiles.Count);
                var suggestions = await aiOrganizer.SuggestMovesAsync(config.LibraryPath, nonConformingFiles, cancellationToken);

                foreach (var suggestion in suggestions)
                {
                    logger.LogInformation("{Action} via AI: {File} \u2192 {Dest}",
                        config.DryRun ? "Would move" : "Moving",
                        Path.GetFileName(suggestion.Source),
                        Path.GetDirectoryName(suggestion.Destination));
                    allOperations.AddRange(await fileMover.MoveFileAsync(suggestion.Source, suggestion.Destination, config.DryRun));
                }
            }
        }

        if (config.RenameNonStandardFiles)
        {
            progress?.Report("Renaming non-standard episode filenames...");
            cancellationToken.ThrowIfCancellationRequested();
            items = ScanAndNormalizeItems();
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
            items = ScanAndNormalizeItems();
            foreach (var item in items)
            {
                if (!Directory.Exists(item.Path))
                    continue;

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
            items = ScanAndNormalizeItems();
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
        } // end if (!isApprovalRun)

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
            UseAiOrganizer = config.UseAiOrganizer,
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
