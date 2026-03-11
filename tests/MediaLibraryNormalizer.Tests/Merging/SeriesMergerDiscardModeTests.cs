using MediaLibraryNormalizer.Config;
using MediaLibraryNormalizer.Matching;
using MediaLibraryNormalizer.Merging;
using MediaLibraryNormalizer.Models;
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

    private static string CreateFile(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, [1, 2, 3, 4]);
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
