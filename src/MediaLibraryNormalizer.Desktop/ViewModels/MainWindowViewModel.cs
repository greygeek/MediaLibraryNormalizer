using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaLibraryNormalizer.Audit;
using MediaLibraryNormalizer.Config;
using MediaLibraryNormalizer.Data;
using MediaLibraryNormalizer.Merging;
using MediaLibraryNormalizer.Models;
using MediaLibraryNormalizer.Runner;

namespace MediaLibraryNormalizer.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly INormalizerRunner _runner;
    private readonly ISeriesAuditRunner _auditRunner;
    private readonly IAppSettingsRepository _settingsRepo;
    private bool _hasFreshPreview;
    private bool _settingsLoaded;

    public ObservableCollection<DuplicateGroupViewModel> DuplicateGroups { get; } = [];
    public ObservableCollection<OperationItemViewModel> Operations { get; } = [];
    public ObservableCollection<ReviewIssueViewModel> ReviewIssues { get; } = [];
    public ObservableCollection<ReviewIssueViewModel> SelectedGroupIssues { get; } = [];
    public ObservableCollection<string> Errors { get; } = [];
    public ObservableCollection<string> ActivityLog { get; } = [];
    public MissingEpisodeFinderViewModel MissingEpisodeFinder { get; }

    public IAsyncRelayCommand PreviewCommand { get; }
    public IAsyncRelayCommand MergeCommand { get; }
    public IRelayCommand NavigateHomeCommand { get; }
    public IRelayCommand NavigateToMergeManagerCommand { get; }
    public IRelayCommand NavigateToMissingEpisodeFinderCommand { get; }
    public IRelayCommand ApproveSelectedGroupCommand { get; }
    public IRelayCommand RemoveSelectedGroupCommand { get; }
    public IRelayCommand ApproveAllActionableCommand { get; }
    public IRelayCommand ClearApprovedGroupsCommand { get; }

    [ObservableProperty]
    private MainWindowPage activePage = MainWindowPage.Home;

    [ObservableProperty]
    private string libraryPath = string.Empty;

    [ObservableProperty]
    private bool exactMatchesWithFilesOnly = true;

    [ObservableProperty]
    private bool discardInferiorDuplicates = true;

    [ObservableProperty]
    private bool useAi;

    [ObservableProperty]
    private bool useHash;

    [ObservableProperty]
    private bool verbose;

    [ObservableProperty]
    private bool deleteSamples;

    [ObservableProperty]
    private bool deleteNonEpisodeFiles;

    [ObservableProperty]
    private bool renameNonStandardFiles;

    [ObservableProperty]
    private bool flattenEpisodeReleaseFolders;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = "Ready.";

    [ObservableProperty]
    private string lastRunMode = "No runs yet";

    [ObservableProperty]
    private int totalFolders;

    [ObservableProperty]
    private int totalFiles;

    [ObservableProperty]
    private int duplicateGroupCount;

    [ObservableProperty]
    private int selectedDuplicateGroupCount;

    [ObservableProperty]
    private int approvedDuplicateGroupCount;

    [ObservableProperty]
    private int operationCount;

    [ObservableProperty]
    private int errorCount;

    [ObservableProperty]
    private int unpackFolderCount;

    [ObservableProperty]
    private int emptyFolderCount;

    [ObservableProperty]
    private int approvedCleanupFolderCount;

    [ObservableProperty]
    private int globalCleanupSweepFolderCount;

    [ObservableProperty]
    private string reportPath = string.Empty;

    [ObservableProperty]
    private string transactionLogPath = string.Empty;

    [ObservableProperty]
    private DuplicateGroupViewModel? selectedDuplicateGroup;

    public MainWindowViewModel()
        : this(new NormalizerRunner(), new SeriesAuditRunner())
    {
    }

    public MainWindowViewModel(INormalizerRunner runner, ISeriesAuditRunner auditRunner)
    {
        _runner = runner;
        _auditRunner = auditRunner;

        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MediaLibraryNormalizer", "audit.db");
        var factory = new AppDbContextFactory(dbPath);
        var repository = new SqliteAuditRepository(factory);
        _settingsRepo = new AppSettingsRepository(factory);
        MissingEpisodeFinder = new MissingEpisodeFinderViewModel(_auditRunner, repository);
        MissingEpisodeFinder.PropertyChanged += OnMissingEpisodeFinderPropertyChanged;

        PreviewCommand = new AsyncRelayCommand(() => ExecuteRunAsync(dryRun: true), CanRunPreview);
        MergeCommand = new AsyncRelayCommand(() => ExecuteRunAsync(dryRun: false), CanRunMerge);
        NavigateHomeCommand = new RelayCommand(() => NavigateTo(MainWindowPage.Home), CanNavigate);
        NavigateToMergeManagerCommand = new RelayCommand(() => NavigateTo(MainWindowPage.MergeManager), CanNavigate);
        NavigateToMissingEpisodeFinderCommand = new RelayCommand(() => NavigateTo(MainWindowPage.MissingEpisodeFinder), CanNavigate);
        ApproveSelectedGroupCommand = new RelayCommand(ApproveSelectedGroup, CanApproveSelectedGroup);
        RemoveSelectedGroupCommand = new RelayCommand(RemoveSelectedGroup, CanRemoveSelectedGroup);
        ApproveAllActionableCommand = new RelayCommand(ApproveAllActionable, CanApproveAllActionable);
        ClearApprovedGroupsCommand = new RelayCommand(ClearApprovedGroups, CanClearApprovedGroups);

        LoadConfig(ConfigLoader.Load());
        _ = InitializeFromDatabaseAsync(factory);
    }

    public string DuplicateGroupsHeader => $"Duplicate Groups ({DuplicateGroups.Count})";

    public bool IsHomePageActive => ActivePage == MainWindowPage.Home;

    public bool IsMergeManagerActive => ActivePage == MainWindowPage.MergeManager;

    public bool IsMissingEpisodeFinderActive => ActivePage == MainWindowPage.MissingEpisodeFinder;

    public string ActiveWorkspaceTitle => ActivePage switch
    {
        MainWindowPage.Home => "Choose a workflow",
        MainWindowPage.MergeManager => "Merge Manager",
        MainWindowPage.MissingEpisodeFinder => "Missing Episode Finder",
        _ => "Media Library Normalizer"
    };

    public string ActiveWorkspaceDescription => ActivePage switch
    {
        MainWindowPage.Home => "Launch the existing merge workflow or the new metadata-driven audit workspace.",
        MainWindowPage.MergeManager => "Preview duplicate groups, approve merges, and execute the existing normalization pipeline.",
        MainWindowPage.MissingEpisodeFinder => "Audit your library against online episode catalogs and review what is missing before any later acquisition workflow exists.",
        _ => string.Empty
    };

    public string HomeStatusSummary => string.IsNullOrWhiteSpace(LibraryPath)
        ? "Set a library path, then choose a workflow tile."
        : $"Library path ready: {LibraryPath}";

    public string MergeManagerTileSummary =>
        "Run dry previews, review duplicate groups, stage approvals, and execute live merges using the current core pipeline.";

    public string MissingEpisodeFinderTileSummary =>
        "Compare local series against online episode catalogs and produce missing-episode reports. Workspace shell only in this phase.";

    public string MissingEpisodeFinderStatus =>
        "Navigation shell is implemented. Catalog lookup, reconciliation, and reporting are the next delivery slice.";

    public string MissingEpisodeFinderReadiness => string.IsNullOrWhiteSpace(LibraryPath)
        ? "Set a shared library path first so the audit workflow can inherit it later."
        : $"The audit workflow will use {LibraryPath} as its initial library root.";

    public string SelectedGroupTitle => SelectedDuplicateGroup?.DisplayName ?? "Select a duplicate group";

    public string SelectedGroupMatchSummary => SelectedDuplicateGroup?.MatchSummary ?? "Run a scan to inspect candidate merges.";

    public string SelectedGroupReviewStatus => SelectedDuplicateGroup?.ReviewStatus ?? "Run a dry run, then approve groups for live merge.";

    public string SelectedGroupCanonicalSummary => SelectedDuplicateGroup is null
        ? string.Empty
        : $"Canonical: {SelectedDuplicateGroup.CanonicalName} ({SelectedDuplicateGroup.CanonicalFileCount} files)";

    public string SelectedGroupCanonicalPath => SelectedDuplicateGroup?.CanonicalPath ?? string.Empty;

    public IReadOnlyList<FolderItemViewModel> SelectedGroupDuplicates => SelectedDuplicateGroup?.DuplicateFolders ?? [];

    public string SelectedGroupEligibilitySummary => SelectedDuplicateGroup?.EligibilitySummary ?? string.Empty;

    public string SelectedGroupIssuesSummary => SelectedGroupIssues.Count == 0
        ? "No folder-level errors recorded for this group."
        : $"{SelectedGroupIssues.Count} folder-level error{(SelectedGroupIssues.Count == 1 ? string.Empty : "s")}";

    public string ReviewQueueSummary =>
        _hasFreshPreview
            ? RequiresApprovalForMerge
                ? $"Actionable groups: {SelectedDuplicateGroupCount} • Approved groups: {ApprovedDuplicateGroupCount}"
                : "No staged approvals required for movie-only operations."
            : "Run a fresh dry run before approving groups for live merge.";

    public string LastRunSummary =>
        $"Folders {TotalFolders} • Files {TotalFiles} • Duplicate groups {DuplicateGroupCount} • Planned/executed operations {OperationCount}";

    public string CleanupSummary =>
        $"Approved-folder cleanup: {ApprovedCleanupFolderCount} • Global empty-folder sweep: {GlobalCleanupSweepFolderCount}";

    partial void OnActivePageChanged(MainWindowPage value)
    {
        OnPropertyChanged(nameof(IsHomePageActive));
        OnPropertyChanged(nameof(IsMergeManagerActive));
        OnPropertyChanged(nameof(IsMissingEpisodeFinderActive));
        OnPropertyChanged(nameof(ActiveWorkspaceTitle));
        OnPropertyChanged(nameof(ActiveWorkspaceDescription));
        RefreshCommandState();
    }

    partial void OnIsBusyChanged(bool value)
    {
        RefreshCommandState();
    }

    partial void OnLibraryPathChanged(string value)
    {
        OnPropertyChanged(nameof(HomeStatusSummary));
        OnPropertyChanged(nameof(MissingEpisodeFinderReadiness));
        MissingEpisodeFinder.LibraryPath = value;
        InvalidateReviewApprovalWorkflow();
        SaveSettings();
    }

    partial void OnExactMatchesWithFilesOnlyChanged(bool value)
    {
        InvalidateReviewApprovalWorkflow();
        SaveSettings();
    }

    partial void OnDiscardInferiorDuplicatesChanged(bool value)
    {
        InvalidateReviewApprovalWorkflow();
        SaveSettings();
    }

    partial void OnUseAiChanged(bool value)
    {
        InvalidateReviewApprovalWorkflow();
        SaveSettings();
    }

    partial void OnUseHashChanged(bool value)
    {
        InvalidateReviewApprovalWorkflow();
        SaveSettings();
    }

    partial void OnVerboseChanged(bool value)
    {
        InvalidateReviewApprovalWorkflow();
        SaveSettings();
    }

    partial void OnDeleteSamplesChanged(bool value)
    {
        InvalidateReviewApprovalWorkflow();
        SaveSettings();
    }

    partial void OnDeleteNonEpisodeFilesChanged(bool value)
    {
        InvalidateReviewApprovalWorkflow();
        SaveSettings();
    }

    partial void OnRenameNonStandardFilesChanged(bool value)
    {
        InvalidateReviewApprovalWorkflow();
        SaveSettings();
    }

    partial void OnFlattenEpisodeReleaseFoldersChanged(bool value)
    {
        InvalidateReviewApprovalWorkflow();
        SaveSettings();
    }

    partial void OnSelectedDuplicateGroupChanged(DuplicateGroupViewModel? value)
    {
        OnPropertyChanged(nameof(SelectedGroupTitle));
        OnPropertyChanged(nameof(SelectedGroupMatchSummary));
        OnPropertyChanged(nameof(SelectedGroupReviewStatus));
        OnPropertyChanged(nameof(SelectedGroupCanonicalSummary));
        OnPropertyChanged(nameof(SelectedGroupCanonicalPath));
        OnPropertyChanged(nameof(SelectedGroupDuplicates));
        OnPropertyChanged(nameof(SelectedGroupEligibilitySummary));
        RefreshSelectedGroupIssues();
        RefreshCommandState();
    }

    private bool CanRunPreview() => !IsBusy && !string.IsNullOrWhiteSpace(LibraryPath);

    private bool CanNavigate() => !IsBusy;

    private bool CanRunMerge() =>
        !IsBusy
        && !string.IsNullOrWhiteSpace(LibraryPath)
        && _hasFreshPreview
        && (!RequiresApprovalForMerge || ApprovedDuplicateGroupCount > 0);

    private bool RequiresApprovalForMerge => DuplicateGroups.Any(group => group.CanStage);

    private bool CanApproveSelectedGroup() =>
        !IsBusy
        && _hasFreshPreview
        && SelectedDuplicateGroup is { CanStage: true, IsStaged: false };

    private bool CanRemoveSelectedGroup() =>
        !IsBusy
        && _hasFreshPreview
        && SelectedDuplicateGroup is { IsStaged: true };

    private bool CanApproveAllActionable() =>
        !IsBusy
        && _hasFreshPreview
        && DuplicateGroups.Any(group => group.CanStage && !group.IsStaged);

    private bool CanClearApprovedGroups() =>
        !IsBusy
        && ApprovedDuplicateGroupCount > 0;

    private void LoadConfig(NormalizerConfig config)
    {
        LibraryPath = config.LibraryPath;
        ExactMatchesWithFilesOnly = config.ExactMatchesWithFilesOnly;
        DiscardInferiorDuplicates = config.DiscardInferiorDuplicates;
        UseAi = config.UseAi;
        UseHash = config.UseHash;
        Verbose = config.Verbose;
        DeleteSamples = config.DeleteSamples;
        MissingEpisodeFinder.LibraryPath = config.LibraryPath;
        MissingEpisodeFinder.Verbose = config.Verbose;
        if (!string.IsNullOrWhiteSpace(config.TheTvdbApiKey))
            MissingEpisodeFinder.TheTvdbApiKey = config.TheTvdbApiKey;
        if (!string.IsNullOrWhiteSpace(config.NzbApiKey))
            MissingEpisodeFinder.NzbApiKey = config.NzbApiKey;
        if (!string.IsNullOrWhiteSpace(config.NzbWatchFolder))
            MissingEpisodeFinder.NzbWatchFolder = config.NzbWatchFolder;
        if (!string.IsNullOrWhiteSpace(config.NzbCategory))
            MissingEpisodeFinder.NzbCategory = config.NzbCategory;
        _hasFreshPreview = false;
    }

    private async Task InitializeFromDatabaseAsync(AppDbContextFactory factory)
    {
        try
        {
            await factory.InitializeAsync();
            var saved = await _settingsRepo.LoadAsync();
            ApplyDbSettings(saved);
            await MissingEpisodeFinder.InitializeAsync();
        }
        catch
        {
            // Startup errors must not crash the app; the JSON-config defaults remain in effect.
        }
    }

    private void ApplyDbSettings(AppSettings saved)
    {
        // DB values override normalizer.config.json defaults when non-empty.
        if (!string.IsNullOrWhiteSpace(saved.LibraryPath))
            LibraryPath = saved.LibraryPath;

        if (!string.IsNullOrWhiteSpace(saved.TheTvdbApiKey))
            MissingEpisodeFinder.TheTvdbApiKey = saved.TheTvdbApiKey;
        if (!string.IsNullOrWhiteSpace(saved.NzbApiKey))
            MissingEpisodeFinder.NzbApiKey = saved.NzbApiKey;
        if (!string.IsNullOrWhiteSpace(saved.NzbWatchFolder))
            MissingEpisodeFinder.NzbWatchFolder = saved.NzbWatchFolder;
        if (!string.IsNullOrWhiteSpace(saved.NzbCategory))
            MissingEpisodeFinder.NzbCategory = saved.NzbCategory;
        if (Enum.TryParse<MediaLibraryNormalizer.Audit.CatalogProviderKind>(saved.CatalogProvider, out var provider))
            MissingEpisodeFinder.SelectedCatalogProvider = provider;

        MissingEpisodeFinder.IncludeSpecials = saved.IncludeSpecials;
        MissingEpisodeFinder.Verbose = saved.Verbose;
        Verbose = saved.Verbose;
        ExactMatchesWithFilesOnly = saved.ExactMatchesWithFilesOnly;
        DiscardInferiorDuplicates = saved.DiscardInferiorDuplicates;
        UseAi = saved.UseAi;
        UseHash = saved.UseHash;
        DeleteSamples = saved.DeleteSamples;
        DeleteNonEpisodeFiles = saved.DeleteNonEpisodeFiles;
        RenameNonStandardFiles = saved.RenameNonStandardFiles;
        FlattenEpisodeReleaseFolders = saved.FlattenEpisodeReleaseFolders;

        _settingsLoaded = true;
    }

    private void SaveSettings()
    {
        if (!_settingsLoaded) return;
        _ = _settingsRepo.SaveAsync(BuildCurrentSettings());
    }

    private AppSettings BuildCurrentSettings() => new()
    {
        LibraryPath = LibraryPath,
        TheTvdbApiKey = MissingEpisodeFinder.TheTvdbApiKey,
        NzbApiKey = MissingEpisodeFinder.NzbApiKey,
        NzbWatchFolder = MissingEpisodeFinder.NzbWatchFolder,
        NzbCategory = MissingEpisodeFinder.NzbCategory,
        CatalogProvider = MissingEpisodeFinder.SelectedCatalogProvider.ToString(),
        IncludeSpecials = MissingEpisodeFinder.IncludeSpecials,
        Verbose = Verbose,
        ExactMatchesWithFilesOnly = ExactMatchesWithFilesOnly,
        DiscardInferiorDuplicates = DiscardInferiorDuplicates,
        UseAi = UseAi,
        UseHash = UseHash,
        DeleteSamples = DeleteSamples,
        DeleteNonEpisodeFiles = DeleteNonEpisodeFiles,
        RenameNonStandardFiles = RenameNonStandardFiles,
        FlattenEpisodeReleaseFolders = FlattenEpisodeReleaseFolders,
    };

    private void OnMissingEpisodeFinderPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is
            nameof(MissingEpisodeFinderViewModel.TheTvdbApiKey) or
            nameof(MissingEpisodeFinderViewModel.NzbApiKey) or
            nameof(MissingEpisodeFinderViewModel.NzbWatchFolder) or
            nameof(MissingEpisodeFinderViewModel.NzbCategory) or
            nameof(MissingEpisodeFinderViewModel.SelectedCatalogProvider) or
            nameof(MissingEpisodeFinderViewModel.IncludeSpecials))
        {
            SaveSettings();
        }
    }

    private async Task ExecuteRunAsync(bool dryRun)
    {
        IsBusy = true;
        StatusMessage = dryRun
            ? "Starting dry run..."
            : RequiresApprovalForMerge
                ? "Starting approved live merge..."
                : "Starting live merge for movie-only operations...";
        ActivityLog.Clear();

        var progress = new Progress<string>(message =>
        {
            StatusMessage = message;
            ActivityLog.Add($"{DateTime.Now:HH:mm:ss}  {message}");
        });

        try
        {
            var config = BuildConfig(dryRun);
            var approvedSeriesKeys = dryRun
                ? null
                : DuplicateGroups
                    .Where(group => group.IsStaged)
                    .Select(group => group.SeriesKey)
                    .ToArray();

            var result = await Task.Run(() => _runner.RunAsync(config, approvedSeriesKeys, progress));

            ApplyResult(result, dryRun);
            StatusMessage = dryRun
                ? RequiresApprovalForMerge
                    ? "Dry run completed. Review and approve groups for live merge."
                    : "Dry run completed. Review planned operations, then run live merge."
                : "Live merge completed.";
            ActivityLog.Add($"{DateTime.Now:HH:mm:ss}  Result written to {result.ReportPath}");
        }
        catch (Exception ex)
        {
            Errors.Clear();
            Errors.Add(ex.Message);
            ErrorCount = Errors.Count;
            StatusMessage = "Run failed.";
            ActivityLog.Add($"{DateTime.Now:HH:mm:ss}  ERROR: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private NormalizerConfig BuildConfig(bool dryRun)
    {
        var baseConfig = ConfigLoader.Load();

        return new NormalizerConfig
        {
            LibraryPath = LibraryPath.Trim(),
            DryRun = dryRun,
            Merge = !dryRun,
            ExactMatchesWithFilesOnly = ExactMatchesWithFilesOnly,
            DiscardInferiorDuplicates = DiscardInferiorDuplicates,
            UseAi = UseAi,
            UseHash = UseHash,
            Verbose = Verbose,
            DeleteSamples = DeleteSamples,
            DeleteNonEpisodeFiles = DeleteNonEpisodeFiles,
            RenameNonStandardFiles = RenameNonStandardFiles,
            FlattenEpisodeReleaseFolders = FlattenEpisodeReleaseFolders,
            AiEndpoint = baseConfig.AiEndpoint,
            AiApiKey = baseConfig.AiApiKey,
            AiModel = baseConfig.AiModel,
            FuzzyThreshold = baseConfig.FuzzyThreshold,
            AiThreshold = baseConfig.AiThreshold,
            HashSizeMB = baseConfig.HashSizeMB,
            MaxConcurrency = baseConfig.MaxConcurrency
        };
    }

    private void ApplyResult(NormalizerRunResult result, bool dryRun)
    {
        foreach (var group in DuplicateGroups)
        {
            group.PropertyChanged -= DuplicateGroupOnPropertyChanged;
        }

        var issueCounts = BuildIssueCounts(result);

        DuplicateGroups.Clear();
        foreach (var group in result.ScanResult.DuplicateGroups
                     .OrderBy(g => g.CanonicalName, StringComparer.OrdinalIgnoreCase))
        {
            var viewModel = new DuplicateGroupViewModel(
                group,
                MergeGroupSelector.IsExactMatchEligibleForApproval(group) || !ExactMatchesWithFilesOnly,
                issueCounts.TryGetValue(group.SeriesKey, out var count) ? count : 0);
            viewModel.PropertyChanged += DuplicateGroupOnPropertyChanged;
            DuplicateGroups.Add(viewModel);
        }

        Operations.Clear();
        foreach (var operation in result.Operations)
        {
            Operations.Add(new OperationItemViewModel(operation));
        }

        ReviewIssues.Clear();
        foreach (var failure in result.CleanupFailures)
        {
            ReviewIssues.Add(CreateReviewIssue(result, failure));
        }

        Errors.Clear();
        foreach (var error in result.ScanResult.Errors)
        {
            Errors.Add(error);
        }

        TotalFolders = result.ScanResult.TotalFolders;
        TotalFiles = result.ScanResult.TotalFiles;
        DuplicateGroupCount = result.ScanResult.DuplicateGroups.Count;
        SelectedDuplicateGroupCount = DuplicateGroups.Count(group => group.CanStage);
        ApprovedDuplicateGroupCount = 0;
        OperationCount = result.Operations.Count;
        ErrorCount = result.ScanResult.Errors.Count;
        UnpackFolderCount = result.ScanResult.UnpackFolders.Count;
        EmptyFolderCount = result.ScanResult.EmptyFolders.Count;
        ApprovedCleanupFolderCount = result.ApprovedCleanupFolderCount;
        GlobalCleanupSweepFolderCount = result.GlobalCleanupSweepFolderCount;
        ReportPath = result.ReportPath ?? string.Empty;
        TransactionLogPath = result.TransactionLogPath ?? string.Empty;
        LastRunMode = result.Config.DryRun ? "Dry run" : "Live merge";
        _hasFreshPreview = dryRun;
        SelectedDuplicateGroup = DuplicateGroups.FirstOrDefault();

        OnPropertyChanged(nameof(DuplicateGroupsHeader));
        OnPropertyChanged(nameof(LastRunSummary));
        OnPropertyChanged(nameof(CleanupSummary));
        OnPropertyChanged(nameof(ReviewQueueSummary));
        RefreshSelectedGroupIssues();
        RefreshCommandState();
    }

    private void ApproveSelectedGroup()
    {
        if (SelectedDuplicateGroup is { CanStage: true } group)
        {
            group.IsStaged = true;
            StatusMessage = $"Approved '{group.DisplayName}' for live merge.";
        }
    }

    private void RemoveSelectedGroup()
    {
        if (SelectedDuplicateGroup is { IsStaged: true } group)
        {
            group.IsStaged = false;
            StatusMessage = $"Removed '{group.DisplayName}' from the staged merge queue.";
        }
    }

    private void ApproveAllActionable()
    {
        foreach (var group in DuplicateGroups.Where(group => group.CanStage))
        {
            group.IsStaged = true;
        }

        StatusMessage = "Approved all actionable groups for live merge.";
        UpdateApprovalCounts();
    }

    private void ClearApprovedGroups()
    {
        foreach (var group in DuplicateGroups.Where(group => group.IsStaged))
        {
            group.IsStaged = false;
        }

        StatusMessage = "Cleared the staged merge queue.";
        UpdateApprovalCounts();
    }

    private void DuplicateGroupOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DuplicateGroupViewModel.IsStaged))
        {
            UpdateApprovalCounts();
            if (ReferenceEquals(sender, SelectedDuplicateGroup))
            {
                OnPropertyChanged(nameof(SelectedGroupReviewStatus));
            }
        }
    }

    private void UpdateApprovalCounts()
    {
        ApprovedDuplicateGroupCount = DuplicateGroups.Count(group => group.IsStaged);
        OnPropertyChanged(nameof(ReviewQueueSummary));
        RefreshCommandState();
    }

    private void RefreshCommandState()
    {
        PreviewCommand.NotifyCanExecuteChanged();
        MergeCommand.NotifyCanExecuteChanged();
        NavigateHomeCommand.NotifyCanExecuteChanged();
        NavigateToMergeManagerCommand.NotifyCanExecuteChanged();
        NavigateToMissingEpisodeFinderCommand.NotifyCanExecuteChanged();
        ApproveSelectedGroupCommand.NotifyCanExecuteChanged();
        RemoveSelectedGroupCommand.NotifyCanExecuteChanged();
        ApproveAllActionableCommand.NotifyCanExecuteChanged();
        ClearApprovedGroupsCommand.NotifyCanExecuteChanged();
    }

    private void NavigateTo(MainWindowPage page)
    {
        if (ActivePage == page)
            return;

        ActivePage = page;
        StatusMessage = page switch
        {
            MainWindowPage.Home => "Ready.",
            MainWindowPage.MergeManager => string.IsNullOrWhiteSpace(LibraryPath)
                ? "Set a library path and run a dry run to inspect duplicate groups."
                : "Merge Manager ready.",
            MainWindowPage.MissingEpisodeFinder => "Missing Episode Finder shell ready. Audit engine implementation is next.",
            _ => StatusMessage
        };
    }

    private void InvalidateReviewApprovalWorkflow()
    {
        _hasFreshPreview = false;

        foreach (var group in DuplicateGroups.Where(static group => group.IsStaged))
        {
            group.IsStaged = false;
        }

        ApprovedDuplicateGroupCount = 0;
        OnPropertyChanged(nameof(ReviewQueueSummary));
        RefreshCommandState();

        if (DuplicateGroups.Count > 0)
        {
            StatusMessage = "Configuration changed. Run a fresh dry run before approving groups for live merge.";
        }
    }

    private Dictionary<string, int> BuildIssueCounts(NormalizerRunResult result)
    {
        return result.CleanupFailures
            .Select(failure => FindGroupForFolder(result.ScanResult.DuplicateGroups, failure.FolderPath))
            .Where(static group => group is not null)
            .GroupBy(group => group!.SeriesKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
    }

    private ReviewIssueViewModel CreateReviewIssue(NormalizerRunResult result, FolderCleanupFailure failure)
    {
        var group = FindGroupForFolder(result.ScanResult.DuplicateGroups, failure.FolderPath);

        return new ReviewIssueViewModel
        {
            SeriesKey = group?.SeriesKey ?? string.Empty,
            GroupName = group?.CanonicalName ?? "Unmapped group",
            FolderPath = failure.FolderPath,
            ExceptionType = failure.ExceptionType,
            Message = failure.Message
        };
    }

    private static SeriesGroup? FindGroupForFolder(IEnumerable<SeriesGroup> groups, string folderPath)
    {
        return groups.FirstOrDefault(group => group.AllFolders.Any(folder =>
            string.Equals(folder.Path, folderPath, StringComparison.OrdinalIgnoreCase)));
    }

    private void RefreshSelectedGroupIssues()
    {
        SelectedGroupIssues.Clear();

        if (SelectedDuplicateGroup is not null)
        {
            foreach (var issue in ReviewIssues.Where(issue =>
                         string.Equals(issue.SeriesKey, SelectedDuplicateGroup.SeriesKey, StringComparison.OrdinalIgnoreCase)))
            {
                SelectedGroupIssues.Add(issue);
            }
        }

        OnPropertyChanged(nameof(SelectedGroupIssuesSummary));
    }
}
