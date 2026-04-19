using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

    // Summary stats
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
                Fill = new SolidColorPaint(new SKColor(139, 92, 246)),
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
                LabelsPaint = new SolidColorPaint(new SKColor(120, 120, 140))
            }
        ];

        DownloadYAxes =
        [
            new Axis
            {
                MinLimit = 0,
                TextSize = 11,
                LabelsPaint = new SolidColorPaint(new SKColor(120, 120, 140))
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
            new(139, 92, 246),   // purple
            new(236, 72, 153),   // pink
            new(59, 130, 246),   // blue
            new(249, 115, 22),   // orange
            new(16, 185, 129),   // green
            new(245, 158, 11),   // amber
            new(99, 102, 241),   // indigo
            new(244, 63, 94),    // rose
            new(14, 165, 233),   // sky
            new(168, 85, 247),   // violet
            new(34, 197, 94),    // emerald
            new(251, 146, 60),   // orange-light
        };

        GenreSeries = genreCounts.Select((g, i) => new PieSeries<int>
        {
            Values = [g.Count],
            Name = g.Genre,
            Fill = new SolidColorPaint(colors[i % colors.Length]),
            MaxRadialColumnWidth = 60
        } as ISeries).ToArray();
    }
}
