using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using MediaLibraryNormalizer.Models;

namespace MediaLibraryNormalizer.Desktop.ViewModels;

public partial class DuplicateGroupViewModel : ViewModelBase
{
    public DuplicateGroupViewModel(SeriesGroup group, bool isActionable, int issueCount)
    {
        SeriesKey = group.SeriesKey;
        DisplayName = group.CanonicalName;
        MatchSummary = group.FuzzyScore.HasValue
            ? $"{group.MatchMethod} • score {group.FuzzyScore.Value}"
            : group.MatchMethod.ToString();
        CanonicalName = group.CanonicalFolder?.OriginalName ?? group.CanonicalName;
        CanonicalPath = group.CanonicalFolder?.Path ?? string.Empty;
        CanonicalFileCount = group.CanonicalFolder?.FileCount ?? 0;
        DuplicateFolders = group.DuplicateFolders
            .Select(folder => new FolderItemViewModel(folder))
            .OrderBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        IsActionable = isActionable;
        CandidateOperationCount = group.PlannedOperations.Count;
        IssueCount = issueCount;
    }

    [ObservableProperty]
    private bool isStaged;

    partial void OnIsStagedChanged(bool value)
    {
        OnPropertyChanged(nameof(ReviewStatus));
    }

    public string SeriesKey { get; }

    public string DisplayName { get; }

    public string MatchSummary { get; }

    public string CanonicalName { get; }

    public string CanonicalPath { get; }

    public int CanonicalFileCount { get; }

    public IReadOnlyList<FolderItemViewModel> DuplicateFolders { get; }

    public bool IsActionable { get; }

    public int CandidateOperationCount { get; }

    public int IssueCount { get; }

    public bool CanStage => IsActionable;

    public string ReviewStatus => !IsActionable
        ? "Not eligible in current mode"
        : IsStaged
            ? "Approved for live merge"
            : "Pending review";

    public string EligibilitySummary => !IsActionable
        ? "Visible for review only — not eligible for staged live merge with the current filters"
        : CandidateOperationCount > 0
            ? $"Eligible for staging • {CandidateOperationCount} planned operation{(CandidateOperationCount == 1 ? string.Empty : "s")}"
            : "Eligible for staging • cleanup-only group (empty duplicate folders)";

    public string IssueSummary => IssueCount == 0
        ? ""
        : $"{IssueCount} cleanup error{(IssueCount == 1 ? string.Empty : "s")} detected";

    public string DuplicateSummary => DuplicateFolders.Count == 1
        ? "1 duplicate folder"
        : $"{DuplicateFolders.Count} duplicate folders";
}
