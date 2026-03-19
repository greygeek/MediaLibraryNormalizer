using MediaLibraryNormalizer.Config;
using MediaLibraryNormalizer.Matching;
using MediaLibraryNormalizer.Merging;
using MediaLibraryNormalizer.Models;
using MediaLibraryNormalizer.Normalization;
using MediaLibraryNormalizer.Parser;
using MediaLibraryNormalizer.Scanner;
using Microsoft.Extensions.Logging.Abstractions;

namespace MediaLibraryNormalizer.Tests.Merging;

public class SeriesMergerDiscardModeTests : IDisposable
{
    private readonly string _tempRoot;

    public SeriesMergerDiscardModeTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "SeriesMergerDiscardTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task MergeAsync_DiscardModeOff_InferiorDuplicateIsSkippedNotDeleted()
    {
        var canonicalDir = CreateDirectory("Canonical");
        var duplicateDir = CreateDirectory("Duplicate");

        var canonicalVideo = CreateFile(canonicalDir, "Show.S01E01.1080p.x265.mkv");
        var duplicateVideo = CreateFile(duplicateDir, "Show.S01E01.720p.x264.mkv");
        CreateFile(duplicateDir, "Show.S01E01.720p.x264.srt");

        var sut = CreateSut(new NormalizerConfig { DiscardInferiorDuplicates = false });
        var group = CreateGroup(canonicalDir, duplicateDir, canonicalVideo, duplicateVideo);

        var ops = await sut.MergeAsync([group], dryRun: true);

        Assert.Empty(ops);
    }

    [Fact]
    public async Task MergeAsync_DiscardModeOn_InferiorDuplicateProducesDeleteOperationsIncludingSidecars()
    {
        var canonicalDir = CreateDirectory("Canonical");
        var duplicateDir = CreateDirectory("Duplicate");

        var canonicalVideo = CreateFile(canonicalDir, "Show.S01E01.1080p.x265.mkv");
        var duplicateVideo = CreateFile(duplicateDir, "Show.S01E01.720p.x264.mkv");
        var duplicateSubtitle = CreateFile(duplicateDir, "Show.S01E01.720p.x264.srt");

        var sut = CreateSut(new NormalizerConfig { DiscardInferiorDuplicates = true });
        var group = CreateGroup(canonicalDir, duplicateDir, canonicalVideo, duplicateVideo);

        var ops = await sut.MergeAsync([group], dryRun: true);

        Assert.Equal(2, ops.Count);
        Assert.All(ops, op => Assert.Equal(OperationType.DeleteDuplicate, op.Type));
        Assert.Contains(ops, op => op.Source == duplicateVideo);
        Assert.Contains(ops, op => op.Source == duplicateSubtitle);
    }

    [Fact]
    public async Task MergeAsync_DiscardModeOn_BetterSourceDeletesCanonicalDuplicateAndMovesSource()
    {
        var canonicalDir = CreateDirectory("Canonical");
        var duplicateDir = CreateDirectory("Duplicate");

        var canonicalVideo = CreateFile(canonicalDir, "Show.S01E01.720p.x264.mkv");
        var duplicateVideo = CreateFile(duplicateDir, "Show.S01E01.1080p.x265.mkv");

        var sut = CreateSut(new NormalizerConfig { DiscardInferiorDuplicates = true });
        var group = CreateGroup(canonicalDir, duplicateDir, canonicalVideo, duplicateVideo);

        var ops = await sut.MergeAsync([group], dryRun: true);

        Assert.Contains(ops, op => op.Type == OperationType.DeleteDuplicate && op.Source == canonicalVideo);
        Assert.Contains(ops, op => op.Type == OperationType.Move && op.Source == duplicateVideo);
    }

    [Fact]
    public async Task MergeAsync_UsesExistingCanonicalSeasonFolderName_ForEquivalentSeasonDirectory()
    {
        var canonicalDir = CreateDirectory("Canonical");
        var canonicalSeasonDir = Directory.CreateDirectory(Path.Combine(canonicalDir, "Season 03")).FullName;
        var duplicateDir = CreateDirectory("Duplicate");
        var duplicateSeasonDir = Directory.CreateDirectory(Path.Combine(duplicateDir, "Season 3")).FullName;

        var canonicalVideo = CreateFile(canonicalSeasonDir, "Show.S03E01.1080p.x265.mkv");
        var duplicateVideo = CreateFile(duplicateSeasonDir, "Show.S03E02.1080p.x265.mkv");

        var sut = CreateSut(new NormalizerConfig());
        var group = CreateGroup(canonicalDir, duplicateDir, canonicalVideo, duplicateVideo, [canonicalSeasonDir]);

        var ops = await sut.MergeAsync([group], dryRun: true);

        Assert.Contains(ops, op =>
            op.Type == OperationType.Move
            && op.Source == duplicateVideo
            && op.Destination == Path.Combine(canonicalSeasonDir, Path.GetFileName(duplicateVideo)));
    }

    [Fact]
    public async Task MergeAsync_UsesExistingSeasonFolderName_WhenSourceIsFlatAndCanonicalHasUnpaddedSeasonSubfolder()
    {
        // Canonical has "Season 1" (no zero-padding); duplicate has files at root (no subfolder).
        // The merged file should land in the existing "Season 1", not a newly created "Season 01".
        var canonicalDir = CreateDirectory("CanonicalUnpaddedSeason");
        var canonicalSeasonDir = Directory.CreateDirectory(Path.Combine(canonicalDir, "Season 1")).FullName;
        var duplicateDir = CreateDirectory("DuplicateFlat");

        var canonicalVideo = CreateFile(canonicalSeasonDir, "Show.S01E01.1080p.x265.mkv");
        var duplicateVideo = CreateFile(duplicateDir, "Show.S01E02.1080p.x265.mkv");

        var sut = CreateSut(new NormalizerConfig());
        var group = CreateGroup(canonicalDir, duplicateDir, canonicalVideo, duplicateVideo, [canonicalSeasonDir]);

        var ops = await sut.MergeAsync([group], dryRun: true);

        Assert.Contains(ops, op =>
            op.Type == OperationType.Move
            && op.Source == duplicateVideo
            && op.Destination == Path.Combine(canonicalSeasonDir, Path.GetFileName(duplicateVideo)));
    }

    [Fact]
    public async Task MergeAsync_UsesPaddedSeasonFolderName_WhenEquivalentCanonicalSeasonFolderMissing()
    {
        var canonicalDir = CreateDirectory("Canonical");
        var duplicateDir = CreateDirectory("Duplicate");
        var duplicateSeasonDir = Directory.CreateDirectory(Path.Combine(duplicateDir, "Season 3")).FullName;

        var duplicateVideo = CreateFile(duplicateSeasonDir, "Show.S03E02.1080p.x265.mkv");

        var sut = CreateSut(new NormalizerConfig());
        var group = CreateGroup(canonicalDir, duplicateDir, null, duplicateVideo);

        var ops = await sut.MergeAsync([group], dryRun: true);

        Assert.Contains(ops, op =>
            op.Type == OperationType.CreateDirectory
            && op.Source == Path.Combine(canonicalDir, "Season 03"));

        Assert.Contains(ops, op =>
            op.Type == OperationType.Move
            && op.Source == duplicateVideo
            && op.Destination == Path.Combine(canonicalDir, "Season 03", Path.GetFileName(duplicateVideo)));
    }

    [Fact]
    public async Task MergeSimilarSubfoldersAsync_MergesEquivalentSeasonFoldersWithinSingleSeriesFolder()
    {
        var seriesDir = CreateDirectory("Show");
        var season02Dir = Directory.CreateDirectory(Path.Combine(seriesDir, "Season 02")).FullName;
        var season2Dir = Directory.CreateDirectory(Path.Combine(seriesDir, "Season 2")).FullName;

        var existingVideo = CreateFile(season02Dir, "Show.S02E01.1080p.x265.mkv");
        var duplicateVideo = CreateFile(season2Dir, "Show.S02E02.1080p.x265.mkv");

        var sut = CreateSut(new NormalizerConfig());
        var item = new MediaItem
        {
            Path = seriesDir,
            OriginalName = "Show",
            NormalizedName = "Show",
            FileCount = 2,
            VideoFiles = [existingVideo, duplicateVideo],
            SeasonFolders = [season02Dir, season2Dir]
        };

        var ops = await sut.MergeSimilarSubfoldersAsync([item], dryRun: true);

        Assert.Contains(ops, op =>
            op.Type == OperationType.Move
            && op.Source == duplicateVideo
            && op.Destination == Path.Combine(season02Dir, Path.GetFileName(duplicateVideo)));
        Assert.Contains(ops, op =>
            op.Type == OperationType.Delete
            && op.Source == season2Dir);
    }

    [Fact]
    public async Task MergeSimilarSubfoldersAsync_RefreshesSeasonFoldersFromDisk_WhenCachedFoldersAreStale()
    {
        var seriesDir = CreateDirectory("ShowWithStaleSeasonFolders");
        var season02Dir = Directory.CreateDirectory(Path.Combine(seriesDir, "Season 02")).FullName;
        var season2Dir = Directory.CreateDirectory(Path.Combine(seriesDir, "Season 2")).FullName;

        var existingVideo = CreateFile(season02Dir, "Show.S02E01.1080p.x265.mkv");
        var duplicateVideo = CreateFile(season2Dir, "Show.S02E02.1080p.x265.mkv");

        var sut = CreateSut(new NormalizerConfig());
        var item = new MediaItem
        {
            Path = seriesDir,
            OriginalName = "Show",
            NormalizedName = "Show",
            FileCount = 2,
            VideoFiles = [existingVideo, duplicateVideo],
            SeasonFolders = [season02Dir]
        };

        var ops = await sut.MergeSimilarSubfoldersAsync([item], dryRun: true);

        Assert.Contains(ops, op =>
            op.Type == OperationType.Move
            && op.Source == duplicateVideo
            && op.Destination == Path.Combine(season02Dir, Path.GetFileName(duplicateVideo)));
        Assert.Contains(ops, op =>
            op.Type == OperationType.Delete
            && op.Source == season2Dir);
    }

    [Fact]
    public async Task MergeSimilarSubfoldersAsync_AhsokaStyleVariants_DeleteAllInferiorCanonicalDuplicates_AndMoveWinningSource()
    {
        var seriesDir = CreateDirectory("Ahsoka");
        var season01Dir = Directory.CreateDirectory(Path.Combine(seriesDir, "Season 01")).FullName;
        var season1Dir = Directory.CreateDirectory(Path.Combine(seriesDir, "Season 1")).FullName;

        var canonicalSameName = CreateFile(season01Dir, "Ahsoka - S01E01 - TBA.mkv", 100);
        var canonicalVariant = CreateFile(season01Dir, "Ahsoka - S01E01 - TBA.2.mkv", 90);
        var sourceWinner = CreateFile(season1Dir, "Ahsoka - S01E01 - TBA.mkv", 200);

        var sut = CreateSut(new NormalizerConfig { DiscardInferiorDuplicates = true });
        var item = new MediaItem
        {
            Path = seriesDir,
            OriginalName = "Ahsoka",
            NormalizedName = "Ahsoka",
            FileCount = 3,
            VideoFiles = [canonicalSameName, canonicalVariant, sourceWinner],
            SeasonFolders = [season01Dir, season1Dir]
        };

        var ops = await sut.MergeSimilarSubfoldersAsync([item], dryRun: true);

        Assert.Contains(ops, op =>
            op.Type == OperationType.DeleteDuplicate
            && op.Source == canonicalSameName);
        Assert.Contains(ops, op =>
            op.Type == OperationType.DeleteDuplicate
            && op.Source == canonicalVariant);
        Assert.Contains(ops, op =>
            op.Type == OperationType.Move
            && op.Source == sourceWinner
            && op.Destination == Path.Combine(season01Dir, Path.GetFileName(sourceWinner)));
        Assert.Contains(ops, op =>
            op.Type == OperationType.Delete
            && op.Source == season1Dir);
    }

    [Fact]
    public async Task MergeSimilarSubfoldersAsync_AhsokaStyleTxtSidecars_AreMovedWithEpisodeFiles()
    {
        var seriesDir = CreateDirectory("AhsokaSidecars");
        var season01Dir = Directory.CreateDirectory(Path.Combine(seriesDir, "Season 01")).FullName;
        var season1Dir = Directory.CreateDirectory(Path.Combine(seriesDir, "Season 1")).FullName;

        var sourceVideo = CreateFile(season1Dir, "Ahsoka - S01E02 - TBA.mkv", 120);
        var sourceTxt = CreateFile(season1Dir, "Ahsoka - S01E02 - TBA.txt", 20);

        var sut = CreateSut(new NormalizerConfig { DiscardInferiorDuplicates = true });
        var item = new MediaItem
        {
            Path = seriesDir,
            OriginalName = "Ahsoka",
            NormalizedName = "Ahsoka",
            FileCount = 1,
            VideoFiles = [sourceVideo],
            SeasonFolders = [season01Dir, season1Dir]
        };

        var ops = await sut.MergeSimilarSubfoldersAsync([item], dryRun: true);

        Assert.Contains(ops, op =>
            op.Type == OperationType.Move
            && op.Source == sourceVideo
            && op.Destination == Path.Combine(season01Dir, Path.GetFileName(sourceVideo)));
        Assert.Contains(ops, op =>
            op.Type == OperationType.Move
            && op.Source == sourceTxt
            && op.Destination == Path.Combine(season01Dir, Path.GetFileName(sourceTxt)));
        Assert.Contains(ops, op =>
            op.Type == OperationType.Delete
            && op.Source == season1Dir);
    }

    [Fact]
    public async Task MergeSimilarSubfoldersAsync_AhsokaStyleResidualTxtFiles_AreDeletedAfterDuplicateVideoRemoval()
    {
        var seriesDir = CreateDirectory("AhsokaResidualTxt");
        var season01Dir = Directory.CreateDirectory(Path.Combine(seriesDir, "Season 01")).FullName;
        var season1Dir = Directory.CreateDirectory(Path.Combine(seriesDir, "Season 1")).FullName;

        var canonicalVideo = CreateFile(season01Dir, "Ahsoka - S01E05 - TBA.mkv", 200);
        var duplicateVideo = CreateFile(season1Dir, "Ahsoka - S01E05 - TBA.10.mkv", 100);
        var residualTxt1 = CreateFile(season1Dir, "Ahsoka - S01E05 - TBA.1.txt", 10);
        var residualTxt2 = CreateFile(season1Dir, "Ahsoka - S01E05 - TBA.2.txt", 10);
        var residualTxt3 = CreateFile(season1Dir, "Ahsoka - S01E05 - TBA.3.txt", 10);

        var sut = CreateSut(new NormalizerConfig { DiscardInferiorDuplicates = true });
        var item = new MediaItem
        {
            Path = seriesDir,
            OriginalName = "Ahsoka",
            NormalizedName = "Ahsoka",
            FileCount = 2,
            VideoFiles = [canonicalVideo, duplicateVideo],
            SeasonFolders = [season01Dir, season1Dir]
        };

        var ops = await sut.MergeSimilarSubfoldersAsync([item], dryRun: true);

        Assert.Contains(ops, op =>
            op.Type == OperationType.DeleteDuplicate
            && op.Source == duplicateVideo);
        Assert.Contains(ops, op =>
            op.Type == OperationType.DeleteDuplicate
            && op.Source == residualTxt1);
        Assert.Contains(ops, op =>
            op.Type == OperationType.DeleteDuplicate
            && op.Source == residualTxt2);
        Assert.Contains(ops, op =>
            op.Type == OperationType.DeleteDuplicate
            && op.Source == residualTxt3);
        Assert.Contains(ops, op =>
            op.Type == OperationType.Delete
            && op.Source == season1Dir);
    }

    [Fact]
    public async Task MergeAsync_MovieDuplicateGroup_KeepsBestMovieMovesItToRootAndDeletesFolders()
    {
        var movieRoot = CreateDirectory("MoviesRoot");
        var betterFolder = Directory.CreateDirectory(Path.Combine(movieRoot, "Dune.Part.Two.2024.1080p.x265")).FullName;
        var worseFolder = Directory.CreateDirectory(Path.Combine(movieRoot, "Dune.Part.Two.2024.720p.x264")).FullName;

        var betterVideo = CreateFile(betterFolder, "Dune.Part.Two.2024.1080p.x265.mkv");
        var worseVideo = CreateFile(worseFolder, "Dune.Part.Two.2024.720p.x264.mkv");

        var sut = CreateSut(new NormalizerConfig());
        var group = new SeriesGroup
        {
            SeriesKey = "Dune Part Two|2024",
            CanonicalName = "Dune Part Two",
            CanonicalFolder = new MediaItem
            {
                Path = betterFolder,
                OriginalName = Path.GetFileName(betterFolder),
                NormalizedName = "Dune Part Two",
                Year = 2024,
                FileCount = 1,
                VideoFiles = [betterVideo],
                Kind = MediaKind.Movie
            },
            AllFolders =
            [
                new MediaItem
                {
                    Path = betterFolder,
                    OriginalName = Path.GetFileName(betterFolder),
                    NormalizedName = "Dune Part Two",
                    Year = 2024,
                    FileCount = 1,
                    VideoFiles = [betterVideo],
                    Kind = MediaKind.Movie
                },
                new MediaItem
                {
                    Path = worseFolder,
                    OriginalName = Path.GetFileName(worseFolder),
                    NormalizedName = "Dune Part Two",
                    Year = 2024,
                    FileCount = 1,
                    VideoFiles = [worseVideo],
                    Kind = MediaKind.Movie
                }
            ],
            MatchMethod = MatchMethod.ExactKey
        };

        var ops = await sut.MergeAsync([group], dryRun: true);

        Assert.Contains(ops, op =>
            op.Type == OperationType.Move
            && op.Source == betterVideo
            && op.Destination == Path.Combine(movieRoot, Path.GetFileName(betterVideo)));
        Assert.Contains(ops, op =>
            op.Type == OperationType.DeleteDuplicate
            && op.Source == worseVideo);
        Assert.Contains(ops, op => op.Type == OperationType.Delete && op.Source == betterFolder);
        Assert.Contains(ops, op => op.Type == OperationType.Delete && op.Source == worseFolder);
    }

    [Fact]
    public async Task FlattenMovieFoldersAsync_MovesMovieToRootAndDeletesFolder()
    {
        var movieRoot = CreateDirectory("StandaloneMovies");
        var movieFolder = Directory.CreateDirectory(Path.Combine(movieRoot, "Arrival.2016")).FullName;
        var movieVideo = CreateFile(movieFolder, "Arrival.2016.1080p.x265.mkv");

        var sut = CreateSut(new NormalizerConfig());
        var item = new MediaItem
        {
            Path = movieFolder,
            OriginalName = "Arrival.2016",
            NormalizedName = "Arrival",
            Year = 2016,
            FileCount = 1,
            VideoFiles = [movieVideo],
            Kind = MediaKind.Movie
        };

        var ops = await sut.FlattenMovieFoldersAsync([item], dryRun: true);

        Assert.Contains(ops, op =>
            op.Type == OperationType.Move
            && op.Source == movieVideo
            && op.Destination == Path.Combine(movieRoot, Path.GetFileName(movieVideo)));
        Assert.Contains(ops, op => op.Type == OperationType.Delete && op.Source == movieFolder);
    }

    [Fact]
    public async Task DeduplicateTopLevelMovieFilesAsync_StripsSabSuffixAndDeletesInferiorDuplicates()
    {
        var movieRoot = CreateDirectory("TopLevelMovies");
        var canonicalVideo = CreateFile(movieRoot, "Gladiator II 2024 1080p x265.mkv");
        var duplicateVideo = CreateFile(movieRoot, "Gladiator II 2024 720p x264.1.mp4");
        var duplicatePoster = CreateFile(movieRoot, "Gladiator II 2024 720p x264.1.jpg");

        var sut = CreateSut(new NormalizerConfig());

        var ops = await sut.DeduplicateTopLevelMovieFilesAsync(movieRoot, dryRun: true);

        Assert.Contains(ops, op => op.Type == OperationType.DeleteDuplicate && op.Source == duplicateVideo);
        Assert.Contains(ops, op => op.Type == OperationType.DeleteDuplicate && op.Source == duplicatePoster);
        Assert.DoesNotContain(ops, op => op.Source == canonicalVideo && op.Type == OperationType.DeleteDuplicate);
    }

    [Fact]
    public async Task DeduplicateTopLevelMovieFilesAsync_RenamesWinningSabSuffixFileAndSidecars()
    {
        var movieRoot = CreateDirectory("TopLevelRename");
        var winningVideo = CreateFile(movieRoot, "Godzilla x Kong - The New Empire 2024 1080p x265.1.mkv");
        var winningNfo = CreateFile(movieRoot, "Godzilla x Kong - The New Empire 2024 1080p x265.1.nfo");
        var winningSubtitle = CreateFile(movieRoot, "Godzilla x Kong - The New Empire 2024 1080p x265.1.srt");
        var losingVideo = CreateFile(movieRoot, "Godzilla x Kong - The New Empire 2024 720p x264.mp4");

        var sut = CreateSut(new NormalizerConfig());

        var ops = await sut.DeduplicateTopLevelMovieFilesAsync(movieRoot, dryRun: true);

        Assert.Contains(ops, op =>
            op.Type == OperationType.Move
            && op.Source == winningVideo
            && op.Destination == Path.Combine(movieRoot, "Godzilla x Kong - The New Empire 2024 1080p x265.mkv"));
        Assert.Contains(ops, op =>
            op.Type == OperationType.Move
            && op.Source == winningSubtitle
            && op.Destination == Path.Combine(movieRoot, "Godzilla x Kong - The New Empire 2024 1080p x265.srt"));
        Assert.Contains(ops, op => op.Type == OperationType.DeleteDuplicate && op.Source == winningNfo);
        Assert.Contains(ops, op => op.Type == OperationType.DeleteDuplicate && op.Source == losingVideo);
    }

    [Fact]
    public async Task DeduplicateTopLevelMovieFilesAsync_PreservesYearInWinnerFileName()
    {
        var movieRoot = CreateDirectory("TopLevelYearRename");
        var winningVideo = CreateFile(movieRoot, "Gladiator II (2024).1.mkv", 200);
        var losingVideo = CreateFile(movieRoot, "Gladiator II (2024).mp4", 100);

        var sut = CreateSut(new NormalizerConfig());

        var ops = await sut.DeduplicateTopLevelMovieFilesAsync(movieRoot, dryRun: true);

        Assert.Contains(ops, op =>
            op.Type == OperationType.Move
            && op.Source == winningVideo
            && op.Destination == Path.Combine(movieRoot, "Gladiator II (2024).mkv"));
        Assert.Contains(ops, op => op.Type == OperationType.DeleteDuplicate && op.Source == losingVideo);
    }

    [Fact]
    public async Task FlattenOrphanedSeriesFoldersAsync_EpisodeTitledContainerWithSeasonSubfolder_MovesToCanonicalSeriesFolder()
    {
        // Simulate: Brooklyn Nine-Nine Defense Rests AAC5 1/Season 3/Brooklyn.Nine-Nine.S03E12.Defense.Rests.mkv
        // Expected: Brooklyn Nine-Nine/Season 3/Brooklyn.Nine-Nine.S03E12.Defense.Rests.mkv
        var libraryRoot = CreateDirectory("FlattenOrphanedLibRoot");

        // Canonical series folder (exists but is flat — no season subfolders yet)
        var canonicalSeriesDir = Directory.CreateDirectory(Path.Combine(libraryRoot, "Brooklyn Nine-Nine")).FullName;

        // Episode-titled container folder with a Season 3 subfolder
        var containerDir = Directory.CreateDirectory(Path.Combine(libraryRoot, "Brooklyn Nine-Nine Defense Rests AAC5 1")).FullName;
        var containerSeason = Directory.CreateDirectory(Path.Combine(containerDir, "Season 3")).FullName;
        var episodeFile = CreateFile(containerSeason, "Brooklyn.Nine-Nine.S03E12.Defense.Rests.1080p.mkv");

        var sut = CreateSut(new NormalizerConfig());

        var canonicalItem = new MediaItem
        {
            Path = canonicalSeriesDir,
            OriginalName = "Brooklyn Nine-Nine",
            NormalizedName = "Brooklyn Nine-Nine",
            Kind = MediaKind.TvSeries,
            VideoFiles = [],
            SeasonFolders = []
        };

        var containerItem = new MediaItem
        {
            Path = containerDir,
            OriginalName = "Brooklyn Nine-Nine Defense Rests AAC5 1",
            NormalizedName = "Brooklyn Nine-Nine Defense Rests AAC5 1",
            Kind = MediaKind.TvSeries,
            VideoFiles = [episodeFile],
            SeasonFolders = [containerSeason]
        };

        var ops = await sut.FlattenOrphanedSeriesFoldersAsync(
            [canonicalItem, containerItem], libraryRoot, dryRun: true);

        var expectedDest = Path.Combine(canonicalSeriesDir, "Season 3", Path.GetFileName(episodeFile));
        Assert.Contains(ops, op =>
            op.Type == OperationType.Move &&
            op.Source == episodeFile &&
            op.Destination == expectedDest);
        Assert.Contains(ops, op =>
            op.Type == OperationType.Delete &&
            op.Source == containerDir);
    }

    [Fact]
    public async Task FlattenOrphanedSeriesFoldersAsync_EpisodeTitledContainer_NoCanonicalFolder_CreatesNewSeriesFolder()
    {
        // No existing "Brooklyn Nine-Nine" folder at root; series title comes from video filename.
        var libraryRoot = CreateDirectory("FlattenOrphanedLibRootNoCanon");

        var containerDir = Directory.CreateDirectory(Path.Combine(libraryRoot, "Brooklyn Nine-Nine Moo Moo AAC5 1")).FullName;
        var containerSeason = Directory.CreateDirectory(Path.Combine(containerDir, "Season 4")).FullName;
        var episodeFile = CreateFile(containerSeason, "Brooklyn.Nine-Nine.S04E16.Moo.Moo.mkv");

        var sut = CreateSut(new NormalizerConfig());

        var containerItem = new MediaItem
        {
            Path = containerDir,
            OriginalName = "Brooklyn Nine-Nine Moo Moo AAC5 1",
            NormalizedName = "Brooklyn Nine-Nine Moo Moo AAC5 1",
            Kind = MediaKind.TvSeries,
            VideoFiles = [episodeFile],
            SeasonFolders = [containerSeason]
        };

        var ops = await sut.FlattenOrphanedSeriesFoldersAsync(
            [containerItem], libraryRoot, dryRun: true);

        // Series title extracted from video filename should be "Brooklyn Nine-Nine"
        var expectedDest = Path.Combine(libraryRoot, "Brooklyn Nine-Nine", "Season 4", Path.GetFileName(episodeFile));
        Assert.Contains(ops, op =>
            op.Type == OperationType.Move &&
            op.Source == episodeFile &&
            op.Destination == expectedDest);
        Assert.Contains(ops, op =>
            op.Type == OperationType.Delete &&
            op.Source == containerDir);
    }

    [Fact]
    public async Task FlattenOrphanedSeriesFoldersAsync_CanonicalSeriesFolder_IsNotMoved()
    {
        // A proper "Brooklyn Nine-Nine/Season 3/episode.mkv" folder must NOT be treated as a
        // container and must not generate any move-away operations.
        var libraryRoot = CreateDirectory("FlattenOrphanedLibRootCanonical");

        var seriesDir = Directory.CreateDirectory(Path.Combine(libraryRoot, "Brooklyn Nine-Nine")).FullName;
        var season3Dir = Directory.CreateDirectory(Path.Combine(seriesDir, "Season 3")).FullName;
        var episodeFile = CreateFile(season3Dir, "Brooklyn.Nine-Nine.S03E01.Full.Boyle.mkv");

        var sut = CreateSut(new NormalizerConfig());

        var item = new MediaItem
        {
            Path = seriesDir,
            OriginalName = "Brooklyn Nine-Nine",
            NormalizedName = "Brooklyn Nine-Nine",
            Kind = MediaKind.TvSeries,
            VideoFiles = [episodeFile],
            SeasonFolders = [season3Dir]
        };

        var ops = await sut.FlattenOrphanedSeriesFoldersAsync([item], libraryRoot, dryRun: true);

        // The canonical folder must not have its episodes moved or be deleted
        Assert.DoesNotContain(ops, op => op.Type == OperationType.Move && op.Source == episodeFile);
        Assert.DoesNotContain(ops, op => op.Type == OperationType.Delete && op.Source == seriesDir);
    }

    private SeriesMerger CreateSut(NormalizerConfig config)
    {
        var detector = new MediaFileDetector();
        var fileMover = new FileMover(
            detector,
            new TransactionLog(NullLogger<TransactionLog>.Instance),
            NullLogger<FileMover>.Instance);

        return new SeriesMerger(
            fileMover,
            detector,
            new TransactionLog(NullLogger<TransactionLog>.Instance),
            new NameNormalizer(),
            new EpisodeParser(),
            new DuplicateDetector(NullLogger<DuplicateDetector>.Instance),
            config,
            NullLogger<SeriesMerger>.Instance);
    }

    private SeriesGroup CreateGroup(
        string canonicalDir,
        string duplicateDir,
        string? canonicalVideo,
        string duplicateVideo,
        List<string>? canonicalSeasonFolders = null)
    {
        var canonical = new MediaItem
        {
            Path = canonicalDir,
            OriginalName = Path.GetFileName(canonicalDir),
            NormalizedName = "Show",
            FileCount = canonicalVideo is null ? 0 : 1,
            VideoFiles = canonicalVideo is null ? [] : [canonicalVideo],
            SeasonFolders = canonicalSeasonFolders ?? []
        };

        var duplicate = new MediaItem
        {
            Path = duplicateDir,
            OriginalName = Path.GetFileName(duplicateDir),
            NormalizedName = "Show",
            FileCount = 1,
            VideoFiles = [duplicateVideo]
        };

        return new SeriesGroup
        {
            SeriesKey = "Show",
            CanonicalName = "Show",
            CanonicalFolder = canonical,
            AllFolders = [canonical, duplicate],
            MatchMethod = MatchMethod.ExactKey
        };
    }

    private string CreateDirectory(string name)
    {
        var path = Path.Combine(_tempRoot, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateFile(string directory, string fileName, int sizeBytes = 4)
    {
        var path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, Enumerable.Repeat((byte)1, sizeBytes).ToArray());
        return path;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
            // best effort cleanup
        }
    }
}
