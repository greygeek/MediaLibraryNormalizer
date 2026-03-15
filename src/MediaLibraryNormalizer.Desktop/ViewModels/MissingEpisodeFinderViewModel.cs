using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaLibraryNormalizer.Audit;

namespace MediaLibraryNormalizer.Desktop.ViewModels;

public partial class MissingEpisodeFinderViewModel : ViewModelBase
{
    private readonly ISeriesAuditRunner _auditRunner;

    public MissingEpisodeFinderViewModel()
        : this(new SeriesAuditRunner())
    {
    }

    public MissingEpisodeFinderViewModel(ISeriesAuditRunner auditRunner)
    {
        _auditRunner = auditRunner;
        RunInventoryCommand = new AsyncRelayCommand(RunInventoryAsync, CanRunInventory);
        ClearInventoryCommand = new RelayCommand(ClearInventory, CanClearInventory);
    }

    public ObservableCollection<SeriesAuditItemViewModel> Series { get; } = [];

    public ObservableCollection<string> ActivityLog { get; } = [];

    public ObservableCollection<string> Errors { get; } = [];

    public IReadOnlyList<CatalogProviderKind> CatalogProviders { get; } = Enum.GetValues<CatalogProviderKind>();

    public IAsyncRelayCommand RunInventoryCommand { get; }

    public IRelayCommand ClearInventoryCommand { get; }

    [ObservableProperty]
    private string libraryPath = string.Empty;

    [ObservableProperty]
    private bool verbose;

    [ObservableProperty]
    private bool includeSpecials;

    [ObservableProperty]
    private CatalogProviderKind selectedCatalogProvider = CatalogProviderKind.None;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = "Ready to scan local inventory.";

    [ObservableProperty]
    private string lastRunSummary = "No inventory runs yet.";

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

    partial void OnIsBusyChanged(bool value)
    {
        RunInventoryCommand.NotifyCanExecuteChanged();
        ClearInventoryCommand.NotifyCanExecuteChanged();
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
    }

    private bool CanRunInventory() => !IsBusy && !string.IsNullOrWhiteSpace(LibraryPath);

    private bool CanClearInventory() => !IsBusy && Series.Count > 0;

    private async Task RunInventoryAsync()
    {
        IsBusy = true;
        StatusMessage = "Scanning local inventory...";
        ActivityLog.Clear();
        Errors.Clear();

        var progress = new Progress<string>(message =>
        {
            StatusMessage = message;
            ActivityLog.Add($"{DateTime.Now:HH:mm:ss}  {message}");
        });

        try
        {
            var result = await _auditRunner.RunAsync(new SeriesAuditOptions
            {
                LibraryPath = LibraryPath.Trim(),
                Verbose = Verbose,
                IncludeSpecials = IncludeSpecials,
                CatalogProvider = SelectedCatalogProvider
            }, progress);

            ApplyResult(result);
            StatusMessage = SelectedCatalogProvider == CatalogProviderKind.None
                ? "Local inventory scan completed. Enable a catalog provider to check for missing episodes."
                : "Inventory and catalog lookup completed.";
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
    }

    private void ClearInventory()
    {
        Series.Clear();
        ActivityLog.Clear();
        Errors.Clear();
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
    }
}