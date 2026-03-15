using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MediaLibraryNormalizer.Audit;

namespace MediaLibraryNormalizer.Desktop.ViewModels;

public class SeriesAuditItemViewModel
{
    public SeriesAuditItemViewModel(SeriesAuditItem item)
    {
        Item = item;
        OriginalTitle = item.OriginalTitle;
        NormalizedTitle = item.NormalizedTitle;
        Year = item.Year;
        TotalVideoFiles = item.TotalVideoFiles;
        SeasonFolderCount = item.SeasonFolderCount;
        ParsedEpisodeCount = item.ParsedEpisodeCount;
        UnparseableFileCount = item.UnparseableFileCount;
        Status = item.Status;
        EpisodePreview = item.EpisodeKeys.Take(10).ToList();
        MissingEpisodePreview = item.MissingEpisodeKeys.Take(10).ToList();

        var unparseableFiles = new List<string>();
        foreach (var path in item.UnparseableFiles)
        {
            if (string.IsNullOrWhiteSpace(path))
                continue;

            unparseableFiles.Add(Path.GetFileName(path) ?? path);
        }

        UnparseableFiles = unparseableFiles
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public string OriginalTitle { get; }

    private SeriesAuditItem Item { get; }

    public string NormalizedTitle { get; }

    public int? Year { get; }

    public int TotalVideoFiles { get; }

    public int SeasonFolderCount { get; }

    public int ParsedEpisodeCount { get; }

    public int UnparseableFileCount { get; }

    public AuditSeriesStatus Status { get; }

    public IReadOnlyList<string> EpisodePreview { get; }

    public IReadOnlyList<string> MissingEpisodePreview { get; }

    public IReadOnlyList<string> UnparseableFiles { get; }

    public string DisplayTitle => Year.HasValue ? $"{NormalizedTitle} ({Year})" : NormalizedTitle;

    public string StatusText => Status switch
    {
        AuditSeriesStatus.ReadyForCatalogLookup => "Ready for catalog lookup",
        AuditSeriesStatus.PartialInventory => "Partial inventory",
        AuditSeriesStatus.NoParsedEpisodes => "No parsed episodes",
        _ => "Unknown"
    };

    public string InventorySummary =>
        $"{ParsedEpisodeCount} parsed episodes • {TotalVideoFiles} video files • {SeasonFolderCount} season folders";

    public string CatalogSummary => Item.CatalogStatus switch
    {
        CatalogLookupStatus.NotRequested => "Catalog lookup disabled for this run.",
        CatalogLookupStatus.Matched => Item.CatalogMatchedYear.HasValue
            ? $"Matched {Item.CatalogMatchedTitle} ({Item.CatalogMatchedYear}) via {Item.CatalogProvider}. Catalog episodes considered: {Item.CatalogEpisodeCount}."
            : $"Matched {Item.CatalogMatchedTitle} via {Item.CatalogProvider}. Catalog episodes considered: {Item.CatalogEpisodeCount}.",
        CatalogLookupStatus.NoMatch => Item.CatalogStatusMessage,
        CatalogLookupStatus.Ambiguous => Item.CatalogStatusMessage,
        CatalogLookupStatus.Error => Item.CatalogStatusMessage,
        _ => string.Empty
    };

    public string MissingSummary => Item.MissingEpisodeCount == 0
        ? "No missing aired episodes detected yet."
        : $"{Item.MissingEpisodeCount} missing aired episode{(Item.MissingEpisodeCount == 1 ? string.Empty : "s")} detected.";

    public string IssueSummary => UnparseableFileCount == 0
        ? "No local parsing issues detected."
        : $"{UnparseableFileCount} file{(UnparseableFileCount == 1 ? string.Empty : "s")} could not be mapped to season/episode.";

    public string EpisodePreviewSummary => EpisodePreview.Count == 0
        ? "No parsed episode keys yet."
        : string.Join(", ", EpisodePreview) + (ParsedEpisodeCount > EpisodePreview.Count ? " ..." : string.Empty);

    public string MissingEpisodePreviewSummary => MissingEpisodePreview.Count == 0
        ? "No missing aired episodes found."
        : string.Join(", ", MissingEpisodePreview) + (Item.MissingEpisodeCount > MissingEpisodePreview.Count ? " ..." : string.Empty);

    public string UnparseableSummary => UnparseableFiles.Count == 0
        ? "No unparseable files."
        : string.Join(Environment.NewLine, UnparseableFiles);
}