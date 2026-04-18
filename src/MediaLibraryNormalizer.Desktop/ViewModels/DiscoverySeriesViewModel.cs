using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using MediaLibraryNormalizer.Audit;

namespace MediaLibraryNormalizer.Desktop.ViewModels;

public partial class DiscoverySeriesViewModel : ObservableObject
{
    public DiscoverySeriesViewModel(DiscoverySeries series)
    {
        SourceId = series.SourceId;
        Title = series.Title;
        Year = series.Year;
        Overview = series.Overview ?? string.Empty;
        ImageUrl = series.ImageUrl;
        Score = series.Score;
        Status = series.Status;
        Country = series.Country;
        FirstAired = series.FirstAired;
        Genres = series.Genres;
    }

    public string SourceId { get; }

    public string Title { get; }

    public int? Year { get; }

    public string Overview { get; }

    public string? ImageUrl { get; }

    public double? Score { get; }

    public string? Status { get; }

    public string? Country { get; }

    public string? FirstAired { get; }

    public IReadOnlyList<string> Genres { get; }

    public string DisplayTitle => Year.HasValue ? $"{Title} ({Year})" : Title;

    public string ScoreText => Score.HasValue ? $"★ {Score.Value:F0}" : string.Empty;

    public string GenreText => Genres.Count > 0 ? string.Join(" · ", Genres) : string.Empty;

    public string StatusText => Status ?? string.Empty;

    public string OverviewTrimmed => Overview.Length > 300 ? Overview[..297] + "..." : Overview;

    [ObservableProperty]
    private bool isInLibrary;

    [ObservableProperty]
    private bool isQueued;

    public string LibraryBadge => IsInLibrary ? "In Library" : IsQueued ? "Queued" : string.Empty;

    partial void OnIsInLibraryChanged(bool value) => OnPropertyChanged(nameof(LibraryBadge));

    partial void OnIsQueuedChanged(bool value) => OnPropertyChanged(nameof(LibraryBadge));
}
