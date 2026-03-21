using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace MediaLibraryNormalizer.Desktop.ViewModels;

/// <summary>Represents the NZB availability check result for a single missing episode.</summary>
public class NzbEpisodeResultViewModel
{
    public NzbEpisodeResultViewModel(
        string episodeKey,
        string episodeTitle,
        int nzbCount,
        bool hasH265,
        int attemptedReleaseCount,
        int sabFailedReleaseCount,
        int availableFreshCount,
        int retryableSabJobCount,
        Func<Task>? clearAttemptHistoryAsync = null,
        Func<Task>? retryFailedSabAsync = null)
    {
        EpisodeKey = episodeKey;
        EpisodeTitle = episodeTitle;
        NzbCount = nzbCount;
        HasH265 = hasH265;
        AttemptedReleaseCount = attemptedReleaseCount;
        SabFailedReleaseCount = sabFailedReleaseCount;
        AvailableFreshCount = availableFreshCount;
        RetryableSabJobCount = retryableSabJobCount;
        ClearAttemptHistoryCommand = clearAttemptHistoryAsync is null
            ? null
            : new AsyncRelayCommand(clearAttemptHistoryAsync, () => attemptedReleaseCount > 0);
        RetryFailedSabCommand = retryFailedSabAsync is null
            ? null
            : new AsyncRelayCommand(retryFailedSabAsync, () => retryableSabJobCount > 0);
    }

    public string EpisodeKey { get; }
    public string EpisodeTitle { get; }
    public int NzbCount { get; }
    public bool HasH265 { get; }
    public int AttemptedReleaseCount { get; }
    public int SabFailedReleaseCount { get; }
    public int AvailableFreshCount { get; }
    public int RetryableSabJobCount { get; }
    public IAsyncRelayCommand? ClearAttemptHistoryCommand { get; }
    public IAsyncRelayCommand? RetryFailedSabCommand { get; }

    public string StatusText => NzbCount > 0
        ? HasH265
            ? $"✓  {NzbCount} NZB{(NzbCount == 1 ? string.Empty : "s")} (H265)"
            : $"✓  {NzbCount} NZB{(NzbCount == 1 ? string.Empty : "s")}"
        : "✗  Not found";

    public string HistoryText
    {
        get
        {
            var parts = new System.Collections.Generic.List<string>();

            if (AttemptedReleaseCount > 0)
                parts.Add($"{AttemptedReleaseCount} sent before");

            if (SabFailedReleaseCount > 0)
                parts.Add($"{SabFailedReleaseCount} failed in SAB");

            if (RetryableSabJobCount > 0)
                parts.Add($"{RetryableSabJobCount} retryable");

            if (NzbCount > 0)
                parts.Add($"{AvailableFreshCount} untried now");

            return string.Join("  •  ", parts);
        }
    }

    public bool HasHistoryText => !string.IsNullOrWhiteSpace(HistoryText);

    public bool CanClearAttemptHistory => AttemptedReleaseCount > 0 && ClearAttemptHistoryCommand is not null;

    public bool CanRetryFailedSabJob => RetryableSabJobCount > 0 && RetryFailedSabCommand is not null;

    public string DisplayLabel => string.IsNullOrWhiteSpace(EpisodeTitle)
        ? EpisodeKey
        : $"{EpisodeKey}  {EpisodeTitle}";
}
