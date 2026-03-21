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
    private readonly INzbDownloadHistory? _nzbDownloadHistory;
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
        _nzbDownloadHistory = repository as INzbDownloadHistory;
        RunInventoryCommand = new AsyncRelayCommand(RunInventoryAsync, CanRunInventory);
        CancelInventoryCommand = new RelayCommand(
            () => RunInventoryCommand.Cancel(),
            () => RunInventoryCommand.IsRunning);
        ClearInventoryCommand = new RelayCommand(ClearInventory, CanClearInventory);
        LoadLastRunCommand = new AsyncRelayCommand(LoadLastRunAsync, CanLoadLastRun);
        CheckUsenetCommand = new AsyncRelayCommand(CheckUsenetAsync, CanCheckUsenet);
        QueueMissingDownloadsCommand = new AsyncRelayCommand(QueueMissingDownloadsAsync, CanQueueMissingDownloads);
        ClearActivityLogCommand = new RelayCommand(ClearActivityLog, CanClearActivityLog);
        ClearEpisodeAttemptHistoryCommand = new AsyncRelayCommand<string>(ClearEpisodeAttemptHistoryAsync, CanClearEpisodeAttemptHistory);
        PendingDeleteSeriesCommand = new RelayCommand(PendingDeleteSeries, CanModifySeries);
        ConfirmDeleteSeriesCommand = new AsyncRelayCommand(ConfirmDeleteSeriesAsync, CanModifySeries);
        CancelDeleteSeriesCommand = new RelayCommand(() => IsPendingDelete = false);
    }

    public ObservableCollection<SeriesAuditItemViewModel> Series { get; } = [];

    public ObservableCollection<LogEntryViewModel> ActivityLog { get; } = [];

    public ObservableCollection<LogEntryViewModel> Errors { get; } = [];

    public ObservableCollection<NzbEpisodeResultViewModel> NzbResults { get; } = [];

    public IReadOnlyList<CatalogProviderKind> CatalogProviders { get; } = Enum.GetValues<CatalogProviderKind>();

    public IAsyncRelayCommand RunInventoryCommand { get; }

    public IRelayCommand CancelInventoryCommand { get; }

    public IRelayCommand ClearInventoryCommand { get; }

    public IAsyncRelayCommand LoadLastRunCommand { get; }

    public IAsyncRelayCommand CheckUsenetCommand { get; }

    public IAsyncRelayCommand QueueMissingDownloadsCommand { get; }

    public IRelayCommand ClearActivityLogCommand { get; }

    public IAsyncRelayCommand<string> ClearEpisodeAttemptHistoryCommand { get; }

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

    [ObservableProperty]
    private string sabnzbdUrl = string.Empty;

    [ObservableProperty]
    private string sabnzbdApiKey = string.Empty;

    public bool IsTheTvdbSelected => SelectedCatalogProvider == CatalogProviderKind.TheTvdb;

    public bool IsNzbSearchConfigured => !string.IsNullOrWhiteSpace(NzbApiKey);

    public bool IsDirectSabQueueConfigured => !string.IsNullOrWhiteSpace(SabnzbdUrl) && !string.IsNullOrWhiteSpace(SabnzbdApiKey);

    public bool IsQueueDownloadsConfigured => IsNzbSearchConfigured && (IsDirectSabQueueConfigured || !string.IsNullOrWhiteSpace(NzbWatchFolder));

    public bool IsSabHistoryConfigured => IsDirectSabQueueConfigured;

    public string QueueModeStatusText => IsDirectSabQueueConfigured
        ? "Queue Mode: Direct SAB API"
        : !string.IsNullOrWhiteSpace(NzbWatchFolder)
            ? "Queue Mode: Watch Folder Fallback"
            : "Queue Mode: Search Only";

    public string QueueModeStatusDetail => IsDirectSabQueueConfigured
        ? "Queue requests go straight to SABnzbd and the returned nzo_id is recorded."
        : !string.IsNullOrWhiteSpace(NzbWatchFolder)
            ? "NZB files are written to the watch folder when direct SAB API access is unavailable."
            : "Usenet search is available, but queueing is disabled until SAB API or a watch folder is configured.";

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

    [ObservableProperty]
    private bool excludeSeasonZeroOnlyMissingSeries;

    [ObservableProperty]
    private int selectedInspectorTabIndex;

    [ObservableProperty]
    private bool followActivityLogTail = true;

    public IEnumerable<SeriesAuditItemViewModel> FilteredSeries =>
        Series.Where(ShouldIncludeSeriesInInventory);

    partial void OnShowOnlyMissingEpisodesChanged(bool value)
    {
        OnPropertyChanged(nameof(FilteredSeries));
        SelectedSeries = FilteredSeries.FirstOrDefault();
    }

    partial void OnExcludeSeasonZeroOnlyMissingSeriesChanged(bool value)
    {
        OnPropertyChanged(nameof(FilteredSeries));
        if (SelectedSeries is not null && !ShouldIncludeSeriesInInventory(SelectedSeries))
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
        ClearActivityLogCommand.NotifyCanExecuteChanged();
        ClearEpisodeAttemptHistoryCommand.NotifyCanExecuteChanged();
        PendingDeleteSeriesCommand.NotifyCanExecuteChanged();
        ConfirmDeleteSeriesCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedCatalogProviderChanged(CatalogProviderKind value)
    {
        OnPropertyChanged(nameof(IsTheTvdbSelected));
    }

    partial void OnNzbApiKeyChanged(string value)
    {
        OnPropertyChanged(nameof(IsNzbSearchConfigured));
        OnPropertyChanged(nameof(IsDirectSabQueueConfigured));
        OnPropertyChanged(nameof(IsQueueDownloadsConfigured));
        OnPropertyChanged(nameof(QueueModeStatusText));
        OnPropertyChanged(nameof(QueueModeStatusDetail));
        OnPropertyChanged(nameof(QueueMissingDownloadsToolTip));
        CheckUsenetCommand.NotifyCanExecuteChanged();
        QueueMissingDownloadsCommand.NotifyCanExecuteChanged();
    }

    partial void OnNzbWatchFolderChanged(string value)
    {
        OnPropertyChanged(nameof(IsQueueDownloadsConfigured));
        OnPropertyChanged(nameof(QueueModeStatusText));
        OnPropertyChanged(nameof(QueueModeStatusDetail));
        OnPropertyChanged(nameof(QueueMissingDownloadsToolTip));
        QueueMissingDownloadsCommand.NotifyCanExecuteChanged();
    }

    partial void OnNzbCategoryChanged(string value)
    {
        OnPropertyChanged(nameof(IsQueueDownloadsConfigured));
    }

    partial void OnSabnzbdUrlChanged(string value)
    {
        OnPropertyChanged(nameof(IsDirectSabQueueConfigured));
        OnPropertyChanged(nameof(IsQueueDownloadsConfigured));
        OnPropertyChanged(nameof(IsSabHistoryConfigured));
        OnPropertyChanged(nameof(QueueModeStatusText));
        OnPropertyChanged(nameof(QueueModeStatusDetail));
        OnPropertyChanged(nameof(QueueMissingDownloadsToolTip));
        QueueMissingDownloadsCommand.NotifyCanExecuteChanged();
    }

    partial void OnSabnzbdApiKeyChanged(string value)
    {
        OnPropertyChanged(nameof(IsDirectSabQueueConfigured));
        OnPropertyChanged(nameof(IsQueueDownloadsConfigured));
        OnPropertyChanged(nameof(IsSabHistoryConfigured));
        OnPropertyChanged(nameof(QueueModeStatusText));
        OnPropertyChanged(nameof(QueueModeStatusDetail));
        OnPropertyChanged(nameof(QueueMissingDownloadsToolTip));
        QueueMissingDownloadsCommand.NotifyCanExecuteChanged();
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
        ClearEpisodeAttemptHistoryCommand.NotifyCanExecuteChanged();
        PendingDeleteSeriesCommand.NotifyCanExecuteChanged();
        ConfirmDeleteSeriesCommand.NotifyCanExecuteChanged();
    }

    private bool CanRunInventory() => !IsBusy && !string.IsNullOrWhiteSpace(LibraryPath);

    private bool ShouldIncludeSeriesInInventory(SeriesAuditItemViewModel series) =>
        (!ShowOnlyMissingEpisodes || series.HasMissingEpisodes)
        && (!ExcludeSeasonZeroOnlyMissingSeries || !series.HasOnlySeasonZeroMissingEpisodes);

    private bool CanClearInventory() => !IsBusy && Series.Count > 0;

    private bool CanLoadLastRun() => !IsBusy && _repository is not null && !string.IsNullOrWhiteSpace(LibraryPath);

    private bool CanCheckUsenet() => !IsBusy && !IsCheckingUsenet && IsNzbSearchConfigured
        && SelectedSeries is { HasMissingEpisodes: true };

    private bool CanQueueMissingDownloads() => !IsBusy && !IsQueueingDownloads && IsQueueDownloadsConfigured
        && SelectedSeries is { HasMissingEpisodes: true };

    private bool CanClearActivityLog() => !IsBusy && ActivityLog.Count > 0;

    public string QueueMissingDownloadsToolTip =>
        !IsNzbSearchConfigured ? "Enter an NZBPlanet API key to enable Usenet search."
        : !IsQueueDownloadsConfigured ? "Enter either SABnzbd URL + API key for direct queueing or a SABnzbd watch folder for file-drop queueing."
        : SelectedSeries is not { HasMissingEpisodes: true } ? "No missing episodes for this series."
        : IsDirectSabQueueConfigured
            ? "Search NZBPlanet, skip previously sent releases and SABnzbd failures, and queue the best release directly in SABnzbd."
            : IsSabHistoryConfigured
                ? "Search NZBPlanet, skip previously sent releases and SABnzbd failures, and save the best NZB to the watch folder."
                : "Search NZBPlanet for each missing episode and save the best NZB to the watch folder.";

    private bool CanModifySeries() => SelectedSeries is not null && !IsBusy;

    private async Task RunInventoryAsync(CancellationToken ct = default)
    {
        IsBusy = true;
        CancelInventoryCommand.NotifyCanExecuteChanged();
        StatusMessage = "Scanning local inventory...";
        AuditProgressValue = 0;
        AuditProgressMax = 0;
        ClearActivityLogEntries();
        ClearErrorEntries();

        var progress = new Progress<AuditProgressReport>(report =>
        {
            StatusMessage = report.Message;
            AddActivity(report.Message);
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
            AddActivity("Audit run cancelled by user.");
        }
        catch (Exception ex)
        {
            AddError(ex.Message);
            StatusMessage = "Inventory scan failed.";
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

        ClearErrorEntries();
        foreach (var error in result.Errors)
        {
            AddError(error, includeInActivityLog: false);
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
        ErrorCount = Errors.Count;
        LastRunSummary =
            SelectedCatalogProvider == CatalogProviderKind.None
                ? $"Series {SeriesCount} • Ready {ReadySeriesCount} • Partial {PartialSeriesCount} • No parsed episodes {NoParsedEpisodeSeriesCount}"
                : $"Series {SeriesCount} • Catalog matched {CatalogMatchedSeriesCount} • Missing episodes {MissingEpisodeCount} • Ambiguous {CatalogAmbiguousSeriesCount}";
        SelectedSeries = FilteredSeries.FirstOrDefault();
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
        ClearActivityLogEntries();
        ClearErrorEntries();
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
        ClearActivityLogEntries();

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
            AddActivity($"Loaded {SeriesCount} series from saved run ({LastSavedRunDate}).");
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed to load last run.";
            AddError(ex.Message);
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
        ClearEpisodeAttemptHistoryCommand.NotifyCanExecuteChanged();
        NzbResults.Clear();
        OnPropertyChanged(nameof(HasNzbResults));
        StatusMessage = $"Checking Usenet availability for {SelectedSeries.DisplayTitle}...";
        AddActivity($"Checking NZBPlanet for {SelectedSeries.DisplayTitle}...");

        try
        {
            var sabFailedReleaseKeysByEpisode = await LoadSabFailedReleaseKeysByEpisodeAsync(SelectedSeries);
            var sabRetryNzoIdsByEpisode = await LoadSabRetryNzoIdsByEpisodeAsync(SelectedSeries);
            using var checker = new NzbPlanetAvailabilityChecker(NzbApiKey.Trim(),
                log: AddActivity);
            foreach (var ep in SelectedSeries.Item.MissingEpisodes)
            {
                var results = await checker.SearchAsync(
                    SelectedSeries.Item.OriginalTitle,
                    ParseSeason(ep.Key),
                    ParseEpisode(ep.Key));

                var attempts = await GetEpisodeAttemptsAsync(SelectedSeries, ep.Key);
                var attemptedReleaseKeys = BuildComparableReleaseKeySet(attempts);
                var sabFailedReleaseKeys = GetReleaseKeysForEpisode(sabFailedReleaseKeysByEpisode, ep.Key);
                var retryableSabNzoIds = GetRetryNzoIdsForEpisode(sabRetryNzoIdsByEpisode, ep.Key);
                var excludedReleaseKeys = BuildComparableReleaseKeySet(attemptedReleaseKeys, sabFailedReleaseKeys);
                var availableFreshCount = results.Count(result => !NzbReleaseIdentity.GetComparableReleaseKeys(result)
                    .Any(excludedReleaseKeys.Contains));

                NzbResults.Add(new NzbEpisodeResultViewModel(
                    ep.Key,
                    ep.Title,
                    results.Count,
                    NzbPlanetAvailabilityChecker.HasH265(results),
                    attempts.Count,
                    sabFailedReleaseKeys.Count,
                    availableFreshCount,
                    retryableSabNzoIds.Count,
                    attempts.Count > 0 ? () => ClearEpisodeAttemptHistoryAsync(ep.Key) : null,
                    retryableSabNzoIds.Count > 0 ? () => RetryFailedSabJobAsync(ep.Key, retryableSabNzoIds) : null));

                AddActivity($"{ep.Key} → {results.Count} NZB(s) found, {availableFreshCount} untried after history filtering.");
            }

            OnPropertyChanged(nameof(HasNzbResults));
            StatusMessage = $"Usenet check complete: {NzbResults.Count(static r => r.NzbCount > 0)}/{NzbResults.Count} episodes available.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Usenet check failed.";
            AddError(ex.Message);
        }
        finally
        {
            IsCheckingUsenet = false;
            CheckUsenetCommand.NotifyCanExecuteChanged();
            ClearEpisodeAttemptHistoryCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task QueueMissingDownloadsAsync()
    {
        if (SelectedSeries is null || string.IsNullOrWhiteSpace(NzbApiKey) || !IsQueueDownloadsConfigured) return;

        IsQueueingDownloads = true;
        ClearEpisodeAttemptHistoryCommand.NotifyCanExecuteChanged();
        var series = SelectedSeries;
        var libraryPath = LibraryPath.Trim();
        var queued = 0;
        var notFound = 0;
        var alreadyAttempted = 0;
        StatusMessage = $"Queuing missing downloads for {series.DisplayTitle}...";
        AddActivity($"Queuing NZBPlanet downloads for {series.DisplayTitle}...");

        try
        {
            var sabFailedReleaseKeysByEpisode = await LoadSabFailedReleaseKeysByEpisodeAsync(series);
            using var checker = new NzbPlanetAvailabilityChecker(NzbApiKey.Trim(),
                log: AddActivity);
            using var sabClient = IsDirectSabQueueConfigured
                ? new SabnzbdHistoryClient(
                    SabnzbdUrl.Trim(),
                    SabnzbdApiKey.Trim(),
                    log: AddActivity)
                : null;

            foreach (var ep in series.Item.MissingEpisodes)
            {
                var results = await checker.SearchAsync(
                    series.Item.OriginalTitle,
                    ParseSeason(ep.Key),
                    ParseEpisode(ep.Key));

                var attempts = await GetEpisodeAttemptsAsync(series, ep.Key);
                var attemptedReleaseKeys = BuildComparableReleaseKeySet(attempts);
                var sabFailedReleaseKeys = GetReleaseKeysForEpisode(sabFailedReleaseKeysByEpisode, ep.Key);
                var excludedReleaseKeys = BuildComparableReleaseKeySet(attemptedReleaseKeys, sabFailedReleaseKeys);

                var preferred = NzbPlanetAvailabilityChecker.SelectPreferred(results, excludedReleaseKeys);
                if (preferred is null || string.IsNullOrWhiteSpace(preferred.DownloadUrl))
                {
                    if (results.Count > 0 && excludedReleaseKeys.Count > 0)
                    {
                        alreadyAttempted++;
                        AddActivity($"{ep.Key} → all {results.Count} NZB(s) were already sent before or failed in SABnzbd.");
                    }
                    else
                    {
                        notFound++;
                        AddActivity($"{ep.Key} → not found on NZBPlanet.");
                    }

                    continue;
                }

                AddActivity($"{ep.Key} → selected: {preferred.Title}");
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

                    if (!queueResult.Success)
                        AddActivity($"{ep.Key} → SABnzbd rejected release: {queueResult.ErrorMessage}");
                }
                else
                {
                    var filename = SanitizeFilename(preferred.Title) + ".nzb";
                    var destPath = Path.Combine(NzbWatchFolder.Trim(), filename);
                    saved = await checker.DownloadNzbAsync(preferred.DownloadUrl, destPath,
                        string.IsNullOrWhiteSpace(NzbCategory) ? null : NzbCategory.Trim());

                    if (saved)
                        AddActivity($"{ep.Key} → saved: {filename}");
                }

                if (saved)
                {
                    queued++;
                    if (_nzbDownloadHistory is not null && !string.IsNullOrWhiteSpace(libraryPath))
                    {
                        await _nzbDownloadHistory.RecordAttemptAsync(
                            libraryPath,
                            series.Item.NormalizedTitle,
                            series.Item.Year,
                            ep.Key,
                            preferred,
                            sabNzoId);
                    }

                    AddActivity(sabClient is not null
                        ? $"{ep.Key} → queued in SABnzbd (nzo_id={sabNzoId ?? "unknown"})."
                        : $"{ep.Key} → queued via watch folder.");
                }
                else
                {
                    notFound++;
                    AddActivity($"{ep.Key} → download failed (see above).");
                }
            }

            StatusMessage = alreadyAttempted > 0
                ? $"Queued {queued} release(s); {alreadyAttempted} episode(s) were skipped because every found release was already attempted."
                : sabClient is not null
                    ? $"Queued {queued} of {queued + notFound} release(s) directly in SABnzbd."
                    : $"Saved {queued} of {queued + notFound} NZBs to {NzbWatchFolder}.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Queue failed.";
            AddError(ex.Message);
        }
        finally
        {
            IsQueueingDownloads = false;
            QueueMissingDownloadsCommand.NotifyCanExecuteChanged();
            ClearEpisodeAttemptHistoryCommand.NotifyCanExecuteChanged();
        }
    }

    private static string SanitizeFilename(string title)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(title.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c)).Trim();
    }

    private async Task<IReadOnlyList<NzbDownloadAttempt>> GetEpisodeAttemptsAsync(
        SeriesAuditItemViewModel series,
        string episodeKey,
        CancellationToken ct = default)
    {
        if (_nzbDownloadHistory is null || string.IsNullOrWhiteSpace(LibraryPath))
            return [];

        return await _nzbDownloadHistory.GetAttemptsAsync(
            LibraryPath.Trim(),
            series.Item.NormalizedTitle,
            series.Item.Year,
            episodeKey,
            ct);
    }

    private bool CanClearEpisodeAttemptHistory(string? episodeKey) =>
        !IsBusy
        && !IsCheckingUsenet
        && !IsQueueingDownloads
        && _nzbDownloadHistory is not null
        && SelectedSeries is not null
        && !string.IsNullOrWhiteSpace(LibraryPath)
        && !string.IsNullOrWhiteSpace(episodeKey);

    private async Task ClearEpisodeAttemptHistoryAsync(string? episodeKey)
    {
        if (!CanClearEpisodeAttemptHistory(episodeKey) || SelectedSeries is null || string.IsNullOrWhiteSpace(episodeKey))
            return;

        await _nzbDownloadHistory!.ClearAttemptsAsync(
            LibraryPath.Trim(),
            SelectedSeries.Item.NormalizedTitle,
            SelectedSeries.Item.Year,
            episodeKey);

        AddActivity($"{episodeKey} → cleared local attempt history.");

        var sabFailedReleaseKeysByEpisode = await LoadSabFailedReleaseKeysByEpisodeAsync(SelectedSeries);
        if (GetReleaseKeysForEpisode(sabFailedReleaseKeysByEpisode, episodeKey).Count > 0)
            AddActivity($"{episodeKey} → SABnzbd still reports failed releases for this episode, so those releases remain filtered.");

        if (CanCheckUsenet())
            await CheckUsenetAsync();
    }

    private async Task<IReadOnlyDictionary<string, IReadOnlyCollection<string>>> LoadSabFailedReleaseKeysByEpisodeAsync(
        SeriesAuditItemViewModel series,
        CancellationToken ct = default)
    {
        if (!IsSabHistoryConfigured)
            return new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase);

        using var client = new SabnzbdHistoryClient(
            SabnzbdUrl.Trim(),
            SabnzbdApiKey.Trim(),
            log: AddActivity);

        var failedHistory = await client.GetFailedHistoryAsync(
            string.IsNullOrWhiteSpace(NzbCategory) ? null : NzbCategory.Trim(),
            ct: ct);

        AddActivity($"SABnzbd history returned {failedHistory.Count} failed item(s).");

        return SabnzbdHistoryMatcher.BuildFailedReleaseKeysByEpisode(
            series.Item.NormalizedTitle,
            series.Item.MissingEpisodeKeys,
            failedHistory);
    }

    private async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> LoadSabRetryNzoIdsByEpisodeAsync(
        SeriesAuditItemViewModel series,
        CancellationToken ct = default)
    {
        if (!IsSabHistoryConfigured)
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        using var client = new SabnzbdHistoryClient(
            SabnzbdUrl.Trim(),
            SabnzbdApiKey.Trim(),
            log: AddActivity);

        var failedHistory = await client.GetFailedHistoryAsync(
            string.IsNullOrWhiteSpace(NzbCategory) ? null : NzbCategory.Trim(),
            ct: ct);

        return SabnzbdHistoryMatcher.BuildFailedNzoIdsByEpisode(
            series.Item.NormalizedTitle,
            series.Item.MissingEpisodeKeys,
            failedHistory);
    }

    private static HashSet<string> BuildComparableReleaseKeySet(IEnumerable<NzbDownloadAttempt> attempts)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var attempt in attempts)
        {
            foreach (var key in NzbReleaseIdentity.GetComparableReleaseKeys(
                attempt.ReleaseTitle,
                attempt.NzbId,
                attempt.ReleaseKey))
            {
                keys.Add(key);
            }
        }

        return keys;
    }

    private static HashSet<string> BuildComparableReleaseKeySet(
        IEnumerable<string> first,
        IEnumerable<string> second)
    {
        var keys = new HashSet<string>(first, StringComparer.OrdinalIgnoreCase);
        keys.UnionWith(second);
        return keys;
    }

    private static IReadOnlyCollection<string> GetReleaseKeysForEpisode(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> releaseKeysByEpisode,
        string episodeKey)
    {
        return releaseKeysByEpisode.TryGetValue(episodeKey, out var keys)
            ? keys
            : [];
    }

    private static IReadOnlyList<string> GetRetryNzoIdsForEpisode(
        IReadOnlyDictionary<string, IReadOnlyList<string>> nzoIdsByEpisode,
        string episodeKey)
    {
        return nzoIdsByEpisode.TryGetValue(episodeKey, out var nzoIds)
            ? nzoIds
            : [];
    }

    private async Task RetryFailedSabJobAsync(string episodeKey, IReadOnlyList<string> nzoIds)
    {
        if (!IsSabHistoryConfigured || nzoIds.Count == 0)
            return;

        using var client = new SabnzbdHistoryClient(
            SabnzbdUrl.Trim(),
            SabnzbdApiKey.Trim(),
            log: AddActivity);

        var nzoId = nzoIds[0];
        var retried = await client.RetryHistoryItemAsync(nzoId);

        if (retried)
        {
            AddActivity($"{episodeKey} → retried failed SABnzbd job {nzoId}.");
            if (CanCheckUsenet())
                await CheckUsenetAsync();
            return;
        }

        AddActivity($"{episodeKey} → SABnzbd did not accept retry for job {nzoId}.");
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
                AddError(msg);
                StatusMessage = "Deletion failed.";
                return;
            }

            if (!Directory.Exists(series.FolderPath))
            {
                AddActivity($"WARNING: Folder not found on disk (already removed?): {series.FolderPath}");
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
                AddActivity($"Deleted: {series.FolderPath}");
            }

            // Capture the current position in the filtered list before modifying the collection,
            // so we can restore focus to the next item rather than jumping to the top.
            var filteredBefore = FilteredSeries.ToList();
            var deletedIndex = filteredBefore.IndexOf(series);
            if (deletedIndex < 0) deletedIndex = 0;

            Series.Remove(series);

            // Select the item that was immediately after the deleted one, falling back to
            // the new last item, so the user's scroll position is preserved.
            var filteredAfter = FilteredSeries.ToList();
            SelectedSeries = filteredAfter.Count > 0 ? filteredAfter[Math.Min(deletedIndex, filteredAfter.Count - 1)] : null;

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
                AddActivity($"Database entry removed for {series.DisplayTitle}.");
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
            AddError(msg);
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

    private void ClearActivityLog()
    {
        ClearActivityLogEntries();
        StatusMessage = "Activity log cleared.";
    }

    private void AddActivity(string message)
    {
        ActivityLog.Add(new LogEntryViewModel(DateTime.Now, message));
        ClearActivityLogCommand.NotifyCanExecuteChanged();
    }

    private void AddError(string message, bool includeInActivityLog = true)
    {
        Errors.Add(new LogEntryViewModel(DateTime.Now, message));
        ErrorCount = Errors.Count;

        if (includeInActivityLog)
            AddActivity($"ERROR: {message}");
    }

    private void ClearActivityLogEntries()
    {
        ActivityLog.Clear();
        ClearActivityLogCommand.NotifyCanExecuteChanged();
    }

    private void ClearErrorEntries()
    {
        Errors.Clear();
        ErrorCount = 0;
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