using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaLibraryNormalizer.Audit;

namespace MediaLibraryNormalizer.Desktop.ViewModels;

public partial class SeriesDiscoveryViewModel : ViewModelBase
{
    private readonly IAuditRepository? _repository;
    private readonly INzbDownloadHistory? _nzbDownloadHistory;
    private readonly HashSet<string> _inventoryTitles = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _inventorySourceIds = new(StringComparer.OrdinalIgnoreCase);

    public SeriesDiscoveryViewModel()
        : this(null)
    {
    }

    public SeriesDiscoveryViewModel(IAuditRepository? repository)
    {
        _repository = repository;
        _nzbDownloadHistory = repository as INzbDownloadHistory;
        BrowseCommand = new AsyncRelayCommand(BrowseAsync, CanBrowse);
        LoadMoreCommand = new AsyncRelayCommand(LoadMoreAsync, CanLoadMore);
        QueueSeriesCommand = new AsyncRelayCommand(QueueSelectedSeriesAsync, CanQueueSeries);
    }

    public ObservableCollection<DiscoveryGenre> Genres { get; } = [];

    public ObservableCollection<DiscoverySeriesViewModel> Results { get; } = [];

    public IAsyncRelayCommand BrowseCommand { get; }

    public IAsyncRelayCommand LoadMoreCommand { get; }

    public IAsyncRelayCommand QueueSeriesCommand { get; }

    [ObservableProperty]
    private DiscoveryGenre? selectedGenre;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isQueueing;

    [ObservableProperty]
    private string statusMessage = "Select a genre and browse popular series.";

    [ObservableProperty]
    private string theTvdbApiKey = string.Empty;

    [ObservableProperty]
    private string nzbApiKey = string.Empty;

    [ObservableProperty]
    private string nzbWatchFolder = string.Empty;

    [ObservableProperty]
    private string nzbCategory = string.Empty;

    [ObservableProperty]
    private string sabnzbdUrl = string.Empty;

    [ObservableProperty]
    private string sabnzbdApiKey = string.Empty;

    [ObservableProperty]
    private DiscoverySeriesViewModel? selectedResult;

    [ObservableProperty]
    private bool hideAlreadyInLibrary = true;

    [ObservableProperty]
    private int currentPage;

    [ObservableProperty]
    private bool hasMorePages;

    public bool IsQueueConfigured =>
        !string.IsNullOrWhiteSpace(NzbApiKey)
        && (!string.IsNullOrWhiteSpace(NzbWatchFolder)
            || (!string.IsNullOrWhiteSpace(SabnzbdUrl) && !string.IsNullOrWhiteSpace(SabnzbdApiKey)));

    public IEnumerable<DiscoverySeriesViewModel> FilteredResults =>
        HideAlreadyInLibrary
            ? Results.Where(static r => !r.IsInLibrary)
            : Results;

    partial void OnHideAlreadyInLibraryChanged(bool value)
    {
        OnPropertyChanged(nameof(FilteredResults));
    }

    partial void OnIsBusyChanged(bool value)
    {
        BrowseCommand.NotifyCanExecuteChanged();
        LoadMoreCommand.NotifyCanExecuteChanged();
        QueueSeriesCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsQueueingChanged(bool value)
    {
        QueueSeriesCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedGenreChanged(DiscoveryGenre? value)
    {
        BrowseCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedResultChanged(DiscoverySeriesViewModel? value)
    {
        QueueSeriesCommand.NotifyCanExecuteChanged();
    }

    public async Task InitializeAsync()
    {
        if (Genres.Count > 0 || string.IsNullOrWhiteSpace(TheTvdbApiKey))
            return;

        await LoadGenresAsync();
        await LoadInventoryTitlesAsync();
    }

    private async Task LoadGenresAsync()
    {
        try
        {
            using var provider = new TheTvdbSeriesCatalogProvider(TheTvdbApiKey.Trim());
            var genres = await provider.GetGenresAsync();
            Genres.Clear();
            foreach (var g in genres)
                Genres.Add(g);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load genres: {ex.Message}";
        }
    }

    private async Task LoadInventoryTitlesAsync()
    {
        if (_repository is null) return;

        try
        {
            var runs = await _repository.GetAllRunsAsync();
            foreach (var run in runs)
            {
                foreach (var item in run.Series)
                {
                    _inventoryTitles.Add(item.NormalizedTitle);
                    if (!string.IsNullOrWhiteSpace(item.CatalogSourceId))
                        _inventorySourceIds.Add(item.CatalogSourceId);
                }
            }
        }
        catch
        {
            // Non-critical — discovery will just not filter existing series.
        }
    }

    private bool CanBrowse() => !IsBusy && SelectedGenre is not null;

    private async Task BrowseAsync()
    {
        if (SelectedGenre is null) return;

        IsBusy = true;
        CurrentPage = 0;
        StatusMessage = $"Browsing {SelectedGenre.Name}...";

        try
        {
            using var provider = new TheTvdbSeriesCatalogProvider(TheTvdbApiKey.Trim());
            var series = await provider.GetSeriesByGenreAsync(SelectedGenre.Id, page: 0);

            Results.Clear();
            foreach (var s in series)
            {
                var vm = new DiscoverySeriesViewModel(s)
                {
                    IsInLibrary = IsAlreadyInLibrary(s)
                };
                Results.Add(vm);
            }

            HasMorePages = series.Count > 0;
            OnPropertyChanged(nameof(FilteredResults));

            var filtered = FilteredResults.Count();
            StatusMessage = $"Found {Results.Count} series for {SelectedGenre.Name} ({filtered} not in library).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Browse failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanLoadMore() => !IsBusy && HasMorePages && SelectedGenre is not null;

    private async Task LoadMoreAsync()
    {
        if (SelectedGenre is null) return;

        IsBusy = true;
        CurrentPage++;
        StatusMessage = $"Loading page {CurrentPage + 1}...";

        try
        {
            using var provider = new TheTvdbSeriesCatalogProvider(TheTvdbApiKey.Trim());
            var series = await provider.GetSeriesByGenreAsync(SelectedGenre.Id, page: CurrentPage);

            foreach (var s in series)
            {
                var vm = new DiscoverySeriesViewModel(s)
                {
                    IsInLibrary = IsAlreadyInLibrary(s)
                };
                Results.Add(vm);
            }

            HasMorePages = series.Count > 0;
            OnPropertyChanged(nameof(FilteredResults));

            var filtered = FilteredResults.Count();
            StatusMessage = $"{Results.Count} series loaded ({filtered} not in library).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Load more failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanQueueSeries() => !IsBusy && !IsQueueing && SelectedResult is not null && IsQueueConfigured;

    private async Task QueueSelectedSeriesAsync()
    {
        if (SelectedResult is null || string.IsNullOrWhiteSpace(NzbApiKey)) return;

        IsQueueing = true;
        var series = SelectedResult;
        StatusMessage = $"Queueing downloads for {series.Title}...";

        try
        {
            // Fetch the full series info with episodes from TVDB
            using var provider = new TheTvdbSeriesCatalogProvider(TheTvdbApiKey.Trim());
            var catalogSeries = await provider.GetSeriesAsync(series.SourceId);

            if (catalogSeries.Episodes.Count == 0)
            {
                StatusMessage = $"No episodes found for {series.Title}.";
                return;
            }

            // Queue all aired episodes via NZBPlanet
            var queued = 0;
            var notFound = 0;
            using var checker = new NzbPlanetAvailabilityChecker(NzbApiKey.Trim());
            using var sabClient = !string.IsNullOrWhiteSpace(SabnzbdUrl) && !string.IsNullOrWhiteSpace(SabnzbdApiKey)
                ? new SabnzbdHistoryClient(SabnzbdUrl.Trim(), SabnzbdApiKey.Trim())
                : null;

            var airedEpisodes = catalogSeries.Episodes
                .Where(e => e.SeasonNumber > 0 && e.AirDate.HasValue && e.AirDate.Value <= DateOnly.FromDateTime(DateTime.Today))
                .ToList();

            foreach (var ep in airedEpisodes)
            {
                var results = await checker.SearchAsync(
                    catalogSeries.Title,
                    ep.SeasonNumber,
                    ep.EpisodeNumber,
                    tvdbId: series.SourceId);

                var preferred = NzbPlanetAvailabilityChecker.SelectPreferred(results);
                if (preferred is null || string.IsNullOrWhiteSpace(preferred.DownloadUrl))
                {
                    notFound++;
                    continue;
                }

                var saved = false;
                string? sabNzoId = null;

                if (sabClient is not null)
                {
                    var queueResult = await sabClient.AddDownloadUrlAsync(
                        preferred.DownloadUrl,
                        preferred.Title,
                        string.IsNullOrWhiteSpace(NzbCategory) ? null : NzbCategory.Trim());
                    saved = queueResult.Success;
                    sabNzoId = queueResult.NzoId;
                }
                else if (!string.IsNullOrWhiteSpace(NzbWatchFolder))
                {
                    var filename = SanitizeFilename(preferred.Title) + ".nzb";
                    var destPath = Path.Combine(NzbWatchFolder.Trim(), filename);
                    saved = await checker.DownloadNzbAsync(preferred.DownloadUrl, destPath,
                        string.IsNullOrWhiteSpace(NzbCategory) ? null : NzbCategory.Trim());
                }

                if (saved) queued++;
                else notFound++;
            }

            series.IsQueued = true;
            StatusMessage = $"Queued {queued} of {airedEpisodes.Count} episode(s) for {series.Title}. {notFound} not found.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Queue failed for {series.Title}: {ex.Message}";
        }
        finally
        {
            IsQueueing = false;
        }
    }

    private bool IsAlreadyInLibrary(DiscoverySeries series)
    {
        if (_inventorySourceIds.Contains(series.SourceId))
            return true;

        return _inventoryTitles.Contains(series.Title);
    }

    private static string SanitizeFilename(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
    }
}
