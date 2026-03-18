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

public partial class MissingEpisodeFinderViewModel : ViewModelBase
{
    private readonly ISeriesAuditRunner _auditRunner;
    private readonly IAuditRepository? _repository;
    private readonly ICatalogCache? _catalogCache;
    private SeriesAuditRunResult? _lastRunResult;

    public MissingEpisodeFinderViewModel()
        : this(new SeriesAuditRunner())
    {
    }

    public MissingEpisodeFinderViewModel(
        ISeriesAuditRunner auditRunner,
        IAuditRepository? repository = null)
    {
        _auditRunner = auditRunner;
        _repository = repository;
        _catalogCache = repository as ICatalogCache;
        RunInventoryCommand = new AsyncRelayCommand(RunInventoryAsync, CanRunInventory);
        CancelInventoryCommand = new RelayCommand(
            () => RunInventoryCommand.Cancel(),
            () => RunInventoryCommand.IsRunning);
        ClearInventoryCommand = new RelayCommand(ClearInventory, CanClearInventory);
        LoadLastRunCommand = new AsyncRelayCommand(LoadLastRunAsync, CanLoadLastRun);
        CheckUsenetCommand = new AsyncRelayCommand(CheckUsenetAsync, CanCheckUsenet);
        QueueMissingDownloadsCommand = new AsyncRelayCommand(QueueMissingDownloadsAsync, CanQueueMissingDownloads);
        PendingDeleteSeriesCommand = new RelayCommand(PendingDeleteSeries, CanModifySeries);
        ConfirmDeleteSeriesCommand = new AsyncRelayCommand(ConfirmDeleteSeriesAsync, CanModifySeries);
        CancelDeleteSeriesCommand = new RelayCommand(() => IsPendingDelete = false);
    }

    public ObservableCollection<SeriesAuditItemViewModel> Series { get; } = [];

    public ObservableCollection<string> ActivityLog { get; } = [];

    public ObservableCollection<string> Errors { get; } = [];

    public ObservableCollection<NzbEpisodeResultViewModel> NzbResults { get; } = [];

    public IReadOnlyList<CatalogProviderKind> CatalogProviders { get; } = Enum.GetValues<CatalogProviderKind>();

    public IAsyncRelayCommand RunInventoryCommand { get; }

    public IRelayCommand CancelInventoryCommand { get; }

    public IRelayCommand ClearInventoryCommand { get; }

    public IAsyncRelayCommand LoadLastRunCommand { get; }

    public IAsyncRelayCommand CheckUsenetCommand { get; }

    public IAsyncRelayCommand QueueMissingDownloadsCommand { get; }

    public IRelayCommand PendingDeleteSeriesCommand { get; }

    public IAsyncRelayCommand ConfirmDeleteSeriesCommand { get; }

    public IRelayCommand CancelDeleteSeriesCommand { get; }

    [ObservableProperty]
    private string libraryPath = string.Empty;

    [ObservableProperty]
    private bool verbose;

    [ObservableProperty]
    private bool includeSpecials;

    [ObservableProperty]
    private CatalogProviderKind selectedCatalogProvider = CatalogProviderKind.None;

    [ObservableProperty]
    private string theTvdbApiKey = string.Empty;

    [ObservableProperty]
    private string nzbApiKey = string.Empty;

    [ObservableProperty]
    private string nzbWatchFolder = string.Empty;

    [ObservableProperty]
    private string nzbCategory = string.Empty;

    public bool IsTheTvdbSelected => SelectedCatalogProvider == CatalogProviderKind.TheTvdb;

    public bool IsNzbConfigured => !string.IsNullOrWhiteSpace(NzbApiKey) && !string.IsNullOrWhiteSpace(NzbWatchFolder);

    public bool HasNzbResults => NzbResults.Count > 0;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isCheckingUsenet;

    [ObservableProperty]
    private bool isQueueingDownloads;

    [ObservableProperty]
    private bool isPendingDelete;

    [ObservableProperty]
    private string statusMessage = "Ready to scan local inventory.";

    [ObservableProperty]
    private string lastRunSummary = "No inventory runs yet.";

    [ObservableProperty]
    private string lastSavedRunDate = string.Empty;

    public bool HasLastSavedRun => !string.IsNullOrEmpty(LastSavedRunDate);

    [ObservableProperty]
    private bool isAuditControlsExpanded = true;

    [ObservableProperty]
    private int auditProgressValue;

    [ObservableProperty]
    private int auditProgressMax;

    partial void OnAuditProgressMaxChanged(int value) =>
        OnPropertyChanged(nameof(IsAuditProgressIndeterminate));

    public bool IsAuditProgressIndeterminate => AuditProgressMax == 0;

    public bool HasResults => SeriesCount > 0;

    public string RunSummaryLine => SeriesCount == 0
        ? "No results — run a scan or load a previous run."
        : MissingEpisodeCount > 0
            ? $"{SeriesCount:N0} series  •  {CatalogMatchedSeriesCount:N0} matched  •  {MissingEpisodeCount:N0} missing  •  {CatalogAmbiguousSeriesCount:N0} ambiguous  •  {UnparseableFileCount:N0} unparseable"
            : $"{SeriesCount:N0} series  •  {ParsedEpisodeCount:N0} parsed episodes  •  {UnparseableFileCount:N0} unparseable";

    [ObservableProperty]
    private int seriesCount;

    [ObservableProperty]
    private int readySeriesCount;

    [ObservableProperty]
    private int partialSeriesCount;

    [ObservableProperty]
    private int noParsedEpisodeSeriesCount;

    [ObservableProperty]
    private int parsedEpisodeCount;

    [ObservableProperty]
    private int unparseableFileCount;

    [ObservableProperty]
    private int catalogMatchedSeriesCount;

    [ObservableProperty]
    private int catalogAmbiguousSeriesCount;

    [ObservableProperty]
    private int catalogErrorSeriesCount;

    [ObservableProperty]
    private int seriesWithMissingEpisodesCount;

    [ObservableProperty]
    private int missingEpisodeCount;

    [ObservableProperty]
    private int errorCount;

    [ObservableProperty]
    private SeriesAuditItemViewModel? selectedSeries;

    public string SelectedSeriesTitle => SelectedSeries?.DisplayTitle ?? "Select a series";

    public string SelectedSeriesSummary => SelectedSeries?.InventorySummary ?? "Run a local inventory scan to inspect series readiness.";

    public string SelectedSeriesIssueSummary => SelectedSeries?.IssueSummary ?? "No series selected.";

    public string SelectedSeriesEpisodePreview => SelectedSeries?.EpisodePreviewSummary ?? "No episode preview available.";

    public string SelectedSeriesCatalogSummary => SelectedSeries?.CatalogSummary ?? "No catalog match details available yet.";

    public string SelectedSeriesMissingSummary => SelectedSeries?.MissingSummary ?? "No missing-episode summary available yet.";

    public string SelectedSeriesMissingEpisodePreview => SelectedSeries?.MissingEpisodePreviewSummary ?? "No missing-episode preview available.";

    public string SelectedSeriesUnparseableSummary => SelectedSeries?.UnparseableSummary ?? "No unparseable files to display.";

    [ObservableProperty]
    private bool showOnlyMissingEpisodes;

    public IEnumerable<SeriesAuditItemViewModel> FilteredSeries =>
        ShowOnlyMissingEpisodes
            ? Series.Where(static s => s.HasMissingEpisodes)
            : Series;

    partial void OnShowOnlyMissingEpisodesChanged(bool value)
    {
        OnPropertyChanged(nameof(FilteredSeries));
        SelectedSeries = FilteredSeries.FirstOrDefault();
    }

    partial void OnIsBusyChanged(bool value)
    {
        RunInventoryCommand.NotifyCanExecuteChanged();
        CancelInventoryCommand.NotifyCanExecuteChanged();
        ClearInventoryCommand.NotifyCanExecuteChanged();
        LoadLastRunCommand.NotifyCanExecuteChanged();
        CheckUsenetCommand.NotifyCanExecuteChanged();
        QueueMissingDownloadsCommand.NotifyCanExecuteChanged();
        PendingDeleteSeriesCommand.NotifyCanExecuteChanged();
        ConfirmDeleteSeriesCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedCatalogProviderChanged(CatalogProviderKind value)
    {
        OnPropertyChanged(nameof(IsTheTvdbSelected));
    }

    partial void OnNzbApiKeyChanged(string value)
    {
        OnPropertyChanged(nameof(IsNzbConfigured));
        OnPropertyChanged(nameof(QueueMissingDownloadsToolTip));
        CheckUsenetCommand.NotifyCanExecuteChanged();
        QueueMissingDownloadsCommand.NotifyCanExecuteChanged();
    }

    partial void OnNzbWatchFolderChanged(string value)
    {
        OnPropertyChanged(nameof(IsNzbConfigured));
        OnPropertyChanged(nameof(QueueMissingDownloadsToolTip));
        QueueMissingDownloadsCommand.NotifyCanExecuteChanged();
    }

    partial void OnNzbCategoryChanged(string value)
    {
        OnPropertyChanged(nameof(IsNzbConfigured));
    }

    partial void OnLibraryPathChanged(string value)
    {
        RunInventoryCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedSeriesChanged(SeriesAuditItemViewModel? value)
    {
        OnPropertyChanged(nameof(SelectedSeriesTitle));
        OnPropertyChanged(nameof(SelectedSeriesSummary));
        OnPropertyChanged(nameof(SelectedSeriesCatalogSummary));
        OnPropertyChanged(nameof(SelectedSeriesMissingSummary));
        OnPropertyChanged(nameof(SelectedSeriesMissingEpisodePreview));
        OnPropertyChanged(nameof(SelectedSeriesIssueSummary));
        OnPropertyChanged(nameof(SelectedSeriesEpisodePreview));
        OnPropertyChanged(nameof(SelectedSeriesUnparseableSummary));
        NzbResults.Clear();
        OnPropertyChanged(nameof(HasNzbResults));
        IsPendingDelete = false;
        OnPropertyChanged(nameof(QueueMissingDownloadsToolTip));
        CheckUsenetCommand.NotifyCanExecuteChanged();
        QueueMissingDownloadsCommand.NotifyCanExecuteChanged();
        PendingDeleteSeriesCommand.NotifyCanExecuteChanged();
        ConfirmDeleteSeriesCommand.NotifyCanExecuteChanged();
    }

    private bool CanRunInventory() => !IsBusy && !string.IsNullOrWhiteSpace(LibraryPath);

    private bool CanClearInventory() => !IsBusy && Series.Count > 0;

    private bool CanLoadLastRun() => !IsBusy && _repository is not null && !string.IsNullOrWhiteSpace(LibraryPath);

    private bool CanCheckUsenet() => !IsBusy && !IsCheckingUsenet && IsNzbConfigured
        && SelectedSeries is { HasMissingEpisodes: true };

    private bool CanQueueMissingDownloads() => !IsBusy && !IsQueueingDownloads && IsNzbConfigured
        && SelectedSeries is { HasMissingEpisodes: true };

    public string QueueMissingDownloadsToolTip =>
        !IsNzbConfigured ? "Enter an NZBPlanet API key and NZB watch folder in Settings to enable this."
        : SelectedSeries is not { HasMissingEpisodes: true } ? "No missing episodes for this series."
        : "Search NZBPlanet for each missing episode and save the best NZB to the watch folder.";

    private bool CanModifySeries() => SelectedSeries is not null && !IsBusy;

    private async Task RunInventoryAsync(CancellationToken ct = default)
    {
        IsBusy = true;
        CancelInventoryCommand.NotifyCanExecuteChanged();
        StatusMessage = "Scanning local inventory...";
        AuditProgressValue = 0;
        AuditProgressMax = 0;
        ActivityLog.Clear();
        Errors.Clear();

        var progress = new Progress<AuditProgressReport>(report =>
        {
            StatusMessage = report.Message;
            ActivityLog.Add($"{DateTime.Now:HH:mm:ss}  {report.Message}");
            AuditProgressMax = report.Total;
            AuditProgressValue = report.Current;
        });

        try
        {
            var result = await _auditRunner.RunAsync(new SeriesAuditOptions
            {
                LibraryPath = LibraryPath.Trim(),
                Verbose = Verbose,
                IncludeSpecials = IncludeSpecials,
                CatalogProvider = SelectedCatalogProvider,
                TheTvdbApiKey = TheTvdbApiKey.Trim(),
                NzbApiKey = NzbApiKey.Trim().Length > 0 ? NzbApiKey.Trim() : null
            }, progress, _catalogCache, ct);

            ApplyResult(result);

            if (_repository is not null)
            {
                await _repository.SaveRunAsync(result);
                LastSavedRunDate = $"{DateTime.Now:yyyy-MM-dd HH:mm}";
                OnPropertyChanged(nameof(HasLastSavedRun));
                LoadLastRunCommand.NotifyCanExecuteChanged();
            }

            StatusMessage = SelectedCatalogProvider == CatalogProviderKind.None
                ? "Local inventory scan completed. Enable a catalog provider to check for missing episodes."
                : "Inventory and catalog lookup completed.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Audit run cancelled.";
            ActivityLog.Add($"{DateTime.Now:HH:mm:ss}  Audit run cancelled by user.");
        }
        catch (Exception ex)
        {
            Errors.Add(ex.Message);
            ErrorCount = Errors.Count;
            StatusMessage = "Inventory scan failed.";
            ActivityLog.Add($"{DateTime.Now:HH:mm:ss}  ERROR: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            CancelInventoryCommand.NotifyCanExecuteChanged();
        }
    }

    private void ApplyResult(SeriesAuditRunResult result)
    {
        Series.Clear();
        foreach (var item in result.Series)
        {
            Series.Add(new SeriesAuditItemViewModel(item));
        }

        Errors.Clear();
        foreach (var error in result.Errors)
        {
            Errors.Add(error);
        }

        SeriesCount = result.Summary.SeriesScanned;
        ReadySeriesCount = result.Summary.ReadySeries;
        PartialSeriesCount = result.Summary.PartialSeries;
        NoParsedEpisodeSeriesCount = result.Summary.NoParsedEpisodeSeries;
        ParsedEpisodeCount = result.Summary.ParsedEpisodeCount;
        UnparseableFileCount = result.Summary.UnparseableFileCount;
        CatalogMatchedSeriesCount = result.Summary.CatalogMatchedSeries;
        CatalogAmbiguousSeriesCount = result.Summary.CatalogAmbiguousSeries;
        CatalogErrorSeriesCount = result.Summary.CatalogErrorSeries;
        SeriesWithMissingEpisodesCount = result.Summary.SeriesWithMissingEpisodes;
        MissingEpisodeCount = result.Summary.MissingEpisodeCount;
        ErrorCount = result.Errors.Count;
        LastRunSummary =
            SelectedCatalogProvider == CatalogProviderKind.None
                ? $"Series {SeriesCount} • Ready {ReadySeriesCount} • Partial {PartialSeriesCount} • No parsed episodes {NoParsedEpisodeSeriesCount}"
                : $"Series {SeriesCount} • Catalog matched {CatalogMatchedSeriesCount} • Missing episodes {MissingEpisodeCount} • Ambiguous {CatalogAmbiguousSeriesCount}";
        SelectedSeries = Series.FirstOrDefault();
        ClearInventoryCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(FilteredSeries));
        IsAuditControlsExpanded = false;
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(RunSummaryLine));
        _lastRunResult = result;
    }

    private void ClearInventory()
    {
        Series.Clear();
        ActivityLog.Clear();
        Errors.Clear();
        NzbResults.Clear();
        SelectedSeries = null;
        SeriesCount = 0;
        ReadySeriesCount = 0;
        PartialSeriesCount = 0;
        NoParsedEpisodeSeriesCount = 0;
        ParsedEpisodeCount = 0;
        UnparseableFileCount = 0;
        CatalogMatchedSeriesCount = 0;
        CatalogAmbiguousSeriesCount = 0;
        CatalogErrorSeriesCount = 0;
        SeriesWithMissingEpisodesCount = 0;
        MissingEpisodeCount = 0;
        ErrorCount = 0;
        LastRunSummary = "No inventory runs yet.";
        StatusMessage = "Inventory results cleared.";
        ClearInventoryCommand.NotifyCanExecuteChanged();
        IsAuditControlsExpanded = true;
        IsPendingDelete = false;
        _lastRunResult = null;
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(RunSummaryLine));
    }

    public Task InitializeAsync() => LoadLastRunAsync();

    private async Task LoadLastRunAsync()
    {
        if (_repository is null) return;

        IsBusy = true;
        StatusMessage = "Loading last saved run...";
        ActivityLog.Clear();

        try
        {
            var stored = await _repository.LoadLatestRunAsync(LibraryPath.Trim());
            if (stored is null)
            {
                StatusMessage = "No saved run found for this library path.";
                return;
            }

            ApplyResult(stored.Value.Result);
            LastSavedRunDate = stored.Value.RunDate.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            OnPropertyChanged(nameof(HasLastSavedRun));
            StatusMessage = $"Loaded run from {LastSavedRunDate}.";
            ActivityLog.Add($"{DateTime.Now:HH:mm:ss}  Loaded {SeriesCount} series from saved run ({LastSavedRunDate}).");
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed to load last run.";
            ActivityLog.Add($"{DateTime.Now:HH:mm:ss}  ERROR: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CheckUsenetAsync()
    {
        if (SelectedSeries is null || string.IsNullOrWhiteSpace(NzbApiKey)) return;

        IsCheckingUsenet = true;
        NzbResults.Clear();
        OnPropertyChanged(nameof(HasNzbResults));
        StatusMessage = $"Checking Usenet availability for {SelectedSeries.DisplayTitle}...";
        ActivityLog.Add($"{DateTime.Now:HH:mm:ss}  Checking NZBPlanet for {SelectedSeries.DisplayTitle}...");

        try
        {
            using var checker = new NzbPlanetAvailabilityChecker(NzbApiKey.Trim(),
                log: msg => ActivityLog.Add($"{DateTime.Now:HH:mm:ss}  {msg}"));
            foreach (var ep in SelectedSeries.Item.MissingEpisodes)
            {
                var results = await checker.SearchAsync(
                    SelectedSeries.Item.OriginalTitle,
                    ParseSeason(ep.Key),
                    ParseEpisode(ep.Key));

                NzbResults.Add(new NzbEpisodeResultViewModel(ep.Key, ep.Title, results.Count, NzbPlanetAvailabilityChecker.HasH265(results)));
                ActivityLog.Add($"{DateTime.Now:HH:mm:ss}  {ep.Key} → {results.Count} NZB(s) found.");
            }

            OnPropertyChanged(nameof(HasNzbResults));
            StatusMessage = $"Usenet check complete: {NzbResults.Count(static r => r.NzbCount > 0)}/{NzbResults.Count} episodes available.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Usenet check failed.";
            ActivityLog.Add($"{DateTime.Now:HH:mm:ss}  ERROR: {ex.Message}");
        }
        finally
        {
            IsCheckingUsenet = false;
            CheckUsenetCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task QueueMissingDownloadsAsync()
    {
        if (SelectedSeries is null || string.IsNullOrWhiteSpace(NzbApiKey) || string.IsNullOrWhiteSpace(NzbWatchFolder)) return;

        IsQueueingDownloads = true;
        var series = SelectedSeries;
        var queued = 0;
        var notFound = 0;
        StatusMessage = $"Queuing missing downloads for {series.DisplayTitle}...";
        ActivityLog.Add($"{DateTime.Now:HH:mm:ss}  Queuing NZBPlanet downloads for {series.DisplayTitle}...");

        try
        {
            using var checker = new NzbPlanetAvailabilityChecker(NzbApiKey.Trim(),
                log: msg => ActivityLog.Add($"{DateTime.Now:HH:mm:ss}  {msg}"));
            foreach (var ep in series.Item.MissingEpisodes)
            {
                var results = await checker.SearchAsync(
                    series.Item.OriginalTitle,
                    ParseSeason(ep.Key),
                    ParseEpisode(ep.Key));

                var preferred = NzbPlanetAvailabilityChecker.SelectPreferred(results);
                if (preferred is null || string.IsNullOrWhiteSpace(preferred.DownloadUrl))
                {
                    notFound++;
                    ActivityLog.Add($"{DateTime.Now:HH:mm:ss}  {ep.Key} → not found on NZBPlanet.");
                    continue;
                }

                var filename = SanitizeFilename(preferred.Title) + ".nzb";
                var destPath = Path.Combine(NzbWatchFolder.Trim(), filename);
                ActivityLog.Add($"{DateTime.Now:HH:mm:ss}  {ep.Key} → selected: {preferred.Title}");
                var saved = await checker.DownloadNzbAsync(preferred.DownloadUrl, destPath,
                    string.IsNullOrWhiteSpace(NzbCategory) ? null : NzbCategory.Trim());
                if (saved)
                {
                    queued++;
                    ActivityLog.Add($"{DateTime.Now:HH:mm:ss}  {ep.Key} → saved: {filename}");
                }
                else
                {
                    notFound++;
                    ActivityLog.Add($"{DateTime.Now:HH:mm:ss}  {ep.Key} → download failed (see above).");
                }
            }

            StatusMessage = $"Saved {queued} of {queued + notFound} NZBs to {NzbWatchFolder}.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Queue failed.";
            ActivityLog.Add($"{DateTime.Now:HH:mm:ss}  ERROR: {ex.Message}");
        }
        finally
        {
            IsQueueingDownloads = false;
            QueueMissingDownloadsCommand.NotifyCanExecuteChanged();
        }
    }

    private static string SanitizeFilename(string title)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(title.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c)).Trim();
    }

    private void PendingDeleteSeries()
    {
        if (SelectedSeries is null) return;
        IsPendingDelete = true;
        StatusMessage = $"Confirm: permanently delete all files for '{SelectedSeries.DisplayTitle}'?";
    }

    private async Task ConfirmDeleteSeriesAsync()
    {
        if (SelectedSeries is null) return;

        var series = SelectedSeries;
        IsPendingDelete = false;
        IsBusy = true;
        StatusMessage = $"Deleting {series.DisplayTitle}...";

        try
        {
            if (string.IsNullOrWhiteSpace(series.FolderPath))
            {
                const string msg = "Folder path is not recorded for this series — cannot delete files.";
                ActivityLog.Add($"{DateTime.Now:HH:mm:ss}  ERROR: {msg}");
                Errors.Add(msg);
                ErrorCount = Errors.Count;
                StatusMessage = "Deletion failed.";
                return;
            }

            if (!Directory.Exists(series.FolderPath))
            {
                ActivityLog.Add($"{DateTime.Now:HH:mm:ss}  WARNING: Folder not found on disk (already removed?): {series.FolderPath}");
            }
            else
            {
                await Task.Run(() =>
                {
                    // Clear read-only attribute on all contained files before deletion;
                    // on Windows these cause Directory.Delete to throw UnauthorizedAccessException.
                    foreach (var file in Directory.EnumerateFiles(series.FolderPath, "*", SearchOption.AllDirectories))
                    {
                        var attrs = File.GetAttributes(file);
                        if ((attrs & FileAttributes.ReadOnly) != 0)
                            File.SetAttributes(file, attrs & ~FileAttributes.ReadOnly);
                    }

                    Directory.Delete(series.FolderPath, recursive: true);
                });
                ActivityLog.Add($"{DateTime.Now:HH:mm:ss}  Deleted: {series.FolderPath}");
            }

            Series.Remove(series);
            SelectedSeries = FilteredSeries.FirstOrDefault();

            if (_repository is not null && _lastRunResult is not null)
            {
                var prunedSeries = _lastRunResult.Series
                    .Where(s => !string.Equals(s.FolderPath, series.FolderPath, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var pruned = new SeriesAuditRunResult
                {
                    LibraryPath = _lastRunResult.LibraryPath,
                    Series = prunedSeries,
                    Errors = _lastRunResult.Errors,
                    Summary = new SeriesAuditSummary
                    {
                        SeriesScanned = prunedSeries.Count,
                        ReadySeries = prunedSeries.Count(s => s.Status == AuditSeriesStatus.ReadyForCatalogLookup),
                        PartialSeries = prunedSeries.Count(s => s.Status == AuditSeriesStatus.PartialInventory),
                        NoParsedEpisodeSeries = prunedSeries.Count(s => s.Status == AuditSeriesStatus.NoParsedEpisodes),
                        ParsedEpisodeCount = prunedSeries.Sum(s => s.ParsedEpisodeCount),
                        UnparseableFileCount = prunedSeries.Sum(s => s.UnparseableFileCount),
                        CatalogMatchedSeries = prunedSeries.Count(s => s.CatalogStatus == CatalogLookupStatus.Matched),
                        CatalogAmbiguousSeries = prunedSeries.Count(s => s.CatalogStatus == CatalogLookupStatus.Ambiguous),
                        CatalogErrorSeries = prunedSeries.Count(s => s.CatalogStatus == CatalogLookupStatus.Error),
                        SeriesWithMissingEpisodes = prunedSeries.Count(s => s.MissingEpisodeCount > 0),
                        MissingEpisodeCount = prunedSeries.Sum(s => s.MissingEpisodeCount),
                    }
                };
                _lastRunResult = pruned;
                await _repository.SaveRunAsync(pruned);
                ActivityLog.Add($"{DateTime.Now:HH:mm:ss}  Database entry removed for {series.DisplayTitle}.");
            }

            StatusMessage = $"Deleted {series.DisplayTitle}.";
            SeriesCount = Series.Count;
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(RunSummaryLine));
            ClearInventoryCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            var msg = $"Failed to delete '{series.DisplayTitle}': {ex.Message}";
            StatusMessage = "Deletion failed.";
            ActivityLog.Add($"{DateTime.Now:HH:mm:ss}  ERROR: {msg}");
            Errors.Add(msg);
            ErrorCount = Errors.Count;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static int ParseSeason(string episodeKey)
    {
        // Key format: S01E02
        if (episodeKey.Length >= 3 && episodeKey[0] == 'S'
            && int.TryParse(episodeKey.AsSpan(1, 2), out var s))
            return s;
        return 1;
    }

    private static int ParseEpisode(string episodeKey)
    {
        // Key format: S01E02
        if (episodeKey.Length >= 6 && episodeKey[3] == 'E'
            && int.TryParse(episodeKey.AsSpan(4, 2), out var e))
            return e;
        return 1;
    }
}