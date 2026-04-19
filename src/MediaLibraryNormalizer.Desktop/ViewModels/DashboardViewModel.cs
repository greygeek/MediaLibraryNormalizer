using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using MediaLibraryNormalizer.Audit;
using SkiaSharp;

namespace MediaLibraryNormalizer.Desktop.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IAuditRepository _auditRepository;
    private readonly INzbDownloadHistory _downloadHistory;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    // Summary stats — row 1
    [ObservableProperty]
    private int totalSeries;

    [ObservableProperty]
    private int catalogMatched;

    [ObservableProperty]
    private int totalMissing;

    [ObservableProperty]
    private int totalDownloads;

    [ObservableProperty]
    private int genreCount;

    // Summary stats — row 2
    [ObservableProperty]
    private int totalEpisodes;

    [ObservableProperty]
    private string completionRate = "—";

    [ObservableProperty]
    private string averageRating = "—";

    [ObservableProperty]
    private string topNetwork = "—";

    [ObservableProperty]
    private int downloadsThisWeek;

    [ObservableProperty]
    private string libraryHealth = "—";

    // Downloads per day chart
    [ObservableProperty]
    private ISeries[] downloadSeries = [];

    [ObservableProperty]
    private ICartesianAxis[] downloadXAxes = [];

    [ObservableProperty]
    private ICartesianAxis[] downloadYAxes = [];

    // Genre composition chart
    [ObservableProperty]
    private ISeries[] genreSeries = [];

    // Status breakdown chart
    [ObservableProperty]
    private ISeries[] statusSeries = [];

    public DashboardViewModel(IAuditRepository auditRepository, INzbDownloadHistory downloadHistory)
    {
        _auditRepository = auditRepository;
        _downloadHistory = downloadHistory;
    }

    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        StatusMessage = "Loading dashboard data…";

        try
        {
            var runsTask = _auditRepository.GetAllRunsAsync();
            var attemptsTask = _downloadHistory.GetAllAttemptsAsync();
            await Task.WhenAll(runsTask, attemptsTask);

            var runs = await runsTask;
            var attempts = await attemptsTask;

            BuildSummaryStats(runs, attempts);
            BuildDownloadsPerDayChart(attempts);
            BuildGenreCompositionChart(runs);
            BuildStatusBreakdownChart(runs);

            StatusMessage = $"Dashboard loaded — {TotalSeries} series, {TotalDownloads} downloads tracked.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load dashboard: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void BuildSummaryStats(IReadOnlyList<SeriesAuditRunResult> runs, IReadOnlyList<NzbDownloadAttempt> attempts)
    {
        var allItems = runs.SelectMany(r => r.Series).ToList();
        TotalSeries = allItems.Count;
        CatalogMatched = allItems.Count(i => i.CatalogStatus == CatalogLookupStatus.Matched);
        TotalMissing = allItems.Sum(i => i.MissingEpisodeCount);
        TotalDownloads = attempts.Count;
        GenreCount = allItems.SelectMany(i => i.CatalogGenres).Distinct(StringComparer.OrdinalIgnoreCase).Count();

        TotalEpisodes = allItems.Sum(i => i.ParsedEpisodeCount);

        var totalCatalogEps = allItems.Where(i => i.CatalogEpisodeCount > 0).Sum(i => i.CatalogEpisodeCount);
        var ownedEps = totalCatalogEps > 0 ? totalCatalogEps - TotalMissing : 0;
        CompletionRate = totalCatalogEps > 0
            ? $"{(double)ownedEps / totalCatalogEps:P0}"
            : "—";

        var rated = allItems.Where(i => i.CatalogRating.HasValue && i.CatalogRating > 0).ToList();
        AverageRating = rated.Count > 0
            ? $"★ {rated.Average(i => i.CatalogRating!.Value):F1}"
            : "—";

        var networkGroups = allItems
            .Where(i => !string.IsNullOrWhiteSpace(i.CatalogNetwork))
            .GroupBy(i => i.CatalogNetwork!)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();
        TopNetwork = networkGroups is not null ? $"{networkGroups.Key} ({networkGroups.Count()})" : "—";

        var weekAgo = DateTimeOffset.UtcNow.AddDays(-7);
        DownloadsThisWeek = attempts.Count(a => a.AttemptedAt >= weekAgo);

        var readyPct = TotalSeries > 0
            ? (double)allItems.Count(i => i.Status == AuditSeriesStatus.ReadyForCatalogLookup) / TotalSeries
            : 0;
        LibraryHealth = TotalSeries > 0
            ? readyPct switch
            {
                >= 0.8 => "Excellent",
                >= 0.6 => "Good",
                >= 0.4 => "Fair",
                _ => "Needs attention"
            }
            : "—";
    }

    private void BuildDownloadsPerDayChart(IReadOnlyList<NzbDownloadAttempt> attempts)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-30);
        var recent = attempts.Where(a => a.AttemptedAt >= cutoff).ToList();

        var grouped = recent
            .GroupBy(a => a.AttemptedAt.Date)
            .ToDictionary(g => g.Key, g => g.Count());

        var labels = new List<string>();
        var values = new List<double>();

        for (int i = 29; i >= 0; i--)
        {
            var date = DateTime.UtcNow.Date.AddDays(-i);
            labels.Add(date.ToString("MMM dd"));
            values.Add(grouped.TryGetValue(date, out var count) ? count : 0);
        }

        DownloadSeries =
        [
            new ColumnSeries<double>
            {
                Values = values,
                Name = "Downloads",
                Fill = new SolidColorPaint(new SKColor(99, 102, 241)),
                MaxBarWidth = 14,
                Rx = 4,
                Ry = 4
            }
        ];

        DownloadXAxes =
        [
            new Axis
            {
                Labels = labels,
                LabelsRotation = 45,
                TextSize = 10,
                LabelsPaint = new SolidColorPaint(new SKColor(100, 116, 139))
            }
        ];

        DownloadYAxes =
        [
            new Axis
            {
                MinLimit = 0,
                TextSize = 11,
                LabelsPaint = new SolidColorPaint(new SKColor(100, 116, 139))
            }
        ];
    }

    private void BuildGenreCompositionChart(IReadOnlyList<SeriesAuditRunResult> runs)
    {
        var allItems = runs.SelectMany(r => r.Series).ToList();
        var genreCounts = allItems
            .SelectMany(i => i.CatalogGenres)
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .GroupBy(g => g, StringComparer.OrdinalIgnoreCase)
            .Select(g => new { Genre = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(12)
            .ToList();

        var colors = new SKColor[]
        {
            new(99, 102, 241),   // indigo (primary)
            new(139, 92, 246),   // violet (secondary)
            new(6, 182, 212),    // cyan
            new(34, 197, 94),    // green
            new(245, 158, 11),   // amber
            new(236, 72, 153),   // pink
            new(249, 115, 22),   // orange
            new(14, 165, 233),   // sky
            new(168, 85, 247),   // purple
            new(244, 63, 94),    // rose
            new(251, 146, 60),   // orange-light
            new(34, 211, 238),   // cyan-light
        };

        GenreSeries = genreCounts.Select((g, i) => new PieSeries<int>
        {
            Values = [g.Count],
            Name = g.Genre,
            Fill = new SolidColorPaint(colors[i % colors.Length]),
            MaxRadialColumnWidth = 60
        } as ISeries).ToArray();
    }

    private void BuildStatusBreakdownChart(IReadOnlyList<SeriesAuditRunResult> runs)
    {
        var allItems = runs.SelectMany(r => r.Series).ToList();
        if (allItems.Count == 0) return;

        var statusGroups = allItems
            .GroupBy(i => i.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ToList();

        var statusColors = new Dictionary<AuditSeriesStatus, SKColor>
        {
            [AuditSeriesStatus.ReadyForCatalogLookup] = new(34, 197, 94),
            [AuditSeriesStatus.PartialInventory] = new(245, 158, 11),
            [AuditSeriesStatus.NoParsedEpisodes] = new(239, 68, 68),
        };

        StatusSeries = statusGroups.Select(g => new PieSeries<int>
        {
            Values = [g.Count],
            Name = g.Status.ToString(),
            Fill = new SolidColorPaint(statusColors.GetValueOrDefault(g.Status, new SKColor(148, 163, 184))),
            MaxRadialColumnWidth = 60
        } as ISeries).ToArray();
    }
}
