using MediaLibraryNormalizer.Config;
using MediaLibraryNormalizer.Merging;
using MediaLibraryNormalizer.Models;

namespace MediaLibraryNormalizer.Tests.Merging;

public class MergeGroupSelectorTests
{
    [Fact]
    public void Select_DefaultMode_ReturnsAllGroups()
    {
        var groups = new List<SeriesGroup>
        {
            CreateGroup(MatchMethod.ExactKey, canonicalFiles: 10, duplicateFiles: [0]),
            CreateGroup(MatchMethod.FuzzyMatch, canonicalFiles: 10, duplicateFiles: [5])
        };

        var config = new NormalizerConfig { ExactMatchesWithFilesOnly = false };

        var selected = MergeGroupSelector.Select(groups, config);

        Assert.Equal(2, selected.Count);
    }

    [Fact]
    public void Select_ExactWithFilesOnly_KeepsOnlyExactGroupsWithRealDuplicateFiles()
    {
        var exactEmptyOnly = CreateGroup(MatchMethod.ExactKey, canonicalFiles: 10, duplicateFiles: [0]);
        var exactWithFiles = CreateGroup(MatchMethod.ExactKey, canonicalFiles: 10, duplicateFiles: [3]);
        var fuzzyWithFiles = CreateGroup(MatchMethod.FuzzyMatch, canonicalFiles: 10, duplicateFiles: [8]);

        var groups = new List<SeriesGroup> { exactEmptyOnly, exactWithFiles, fuzzyWithFiles };
        var config = new NormalizerConfig { ExactMatchesWithFilesOnly = true };

        var selected = MergeGroupSelector.Select(groups, config);

        Assert.Single(selected);
        Assert.Same(exactWithFiles, selected[0]);
    }

    [Fact]
    public void Select_WithApprovedSeriesKeys_FiltersBaseSelection()
    {
        var approved = CreateGroup(MatchMethod.ExactKey, canonicalFiles: 10, duplicateFiles: [3], seriesKey: "Approved");
        var unapproved = CreateGroup(MatchMethod.ExactKey, canonicalFiles: 10, duplicateFiles: [2], seriesKey: "Unapproved");
        var fuzzy = CreateGroup(MatchMethod.FuzzyMatch, canonicalFiles: 10, duplicateFiles: [4], seriesKey: "Fuzzy");

        var groups = new List<SeriesGroup> { approved, unapproved, fuzzy };
        var config = new NormalizerConfig { ExactMatchesWithFilesOnly = true };

        var selected = MergeGroupSelector.Select(groups, config, ["Approved", "Fuzzy"]);

        Assert.Single(selected);
        Assert.Same(approved, selected[0]);
    }

    [Fact]
    public void SelectForApprovalReview_ExactWithFilesOnly_IncludesExactEmptyGroups()
    {
        var exactEmptyOnly = CreateGroup(MatchMethod.ExactKey, canonicalFiles: 10, duplicateFiles: [0], seriesKey: "ExactEmpty");
        var exactWithFiles = CreateGroup(MatchMethod.ExactKey, canonicalFiles: 10, duplicateFiles: [3], seriesKey: "ExactFiles");
        var fuzzy = CreateGroup(MatchMethod.FuzzyMatch, canonicalFiles: 10, duplicateFiles: [4], seriesKey: "Fuzzy");

        var groups = new List<SeriesGroup> { exactEmptyOnly, exactWithFiles, fuzzy };
        var config = new NormalizerConfig { ExactMatchesWithFilesOnly = true };

        var selected = MergeGroupSelector.SelectForApprovalReview(groups, config);

        Assert.Equal(2, selected.Count);
        Assert.Contains(exactEmptyOnly, selected);
        Assert.Contains(exactWithFiles, selected);
        Assert.DoesNotContain(fuzzy, selected);
    }

    [Fact]
    public void IsExactMatchWithRealFiles_ReturnsTrue_WhenDuplicateHasVideoFiles()
    {
        var group = CreateGroup(MatchMethod.ExactKey, canonicalFiles: 15, duplicateFiles: [2, 0]);

        Assert.True(MergeGroupSelector.IsExactMatchWithRealFiles(group));
    }

    [Fact]
    public void IsExactMatchWithRealFiles_ReturnsFalse_ForExactGroupWithOnlyEmptyDuplicates()
    {
        var group = CreateGroup(MatchMethod.ExactKey, canonicalFiles: 15, duplicateFiles: [0, 0]);

        Assert.False(MergeGroupSelector.IsExactMatchWithRealFiles(group));
    }

    [Fact]
    public void IsExactMatchWithRealFiles_ReturnsFalse_ForFuzzyGroupEvenWithFiles()
    {
        var group = CreateGroup(MatchMethod.FuzzyMatch, canonicalFiles: 15, duplicateFiles: [4]);

        Assert.False(MergeGroupSelector.IsExactMatchWithRealFiles(group));
    }

    private static SeriesGroup CreateGroup(MatchMethod method, int canonicalFiles, List<int> duplicateFiles, string seriesKey = "Canonical")
    {
        var canonical = new MediaItem
        {
            Path = "D:/TV/Canonical",
            OriginalName = "Canonical",
            NormalizedName = "Canonical",
            FileCount = canonicalFiles
        };

        var folders = new List<MediaItem> { canonical };
        for (var i = 0; i < duplicateFiles.Count; i++)
        {
            folders.Add(new MediaItem
            {
                Path = $"D:/TV/Dupe-{i}",
                OriginalName = $"Dupe-{i}",
                NormalizedName = "Canonical",
                FileCount = duplicateFiles[i]
            });
        }

        return new SeriesGroup
        {
            SeriesKey = seriesKey,
            CanonicalFolder = canonical,
            CanonicalName = "Canonical",
            AllFolders = folders,
            MatchMethod = method
        };
    }
}
