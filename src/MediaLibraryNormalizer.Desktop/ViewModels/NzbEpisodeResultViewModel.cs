namespace MediaLibraryNormalizer.Desktop.ViewModels;

/// <summary>Represents the NZB availability check result for a single missing episode.</summary>
public class NzbEpisodeResultViewModel
{
    public NzbEpisodeResultViewModel(string episodeKey, string episodeTitle, int nzbCount)
    {
        EpisodeKey = episodeKey;
        EpisodeTitle = episodeTitle;
        NzbCount = nzbCount;
    }

    public string EpisodeKey { get; }

    public string EpisodeTitle { get; }

    public int NzbCount { get; }

    public string StatusText => NzbCount > 0
        ? $"✓  {NzbCount} NZB{(NzbCount == 1 ? string.Empty : "s")} found"
        : "✗  Not found";

    public string DisplayLabel => string.IsNullOrWhiteSpace(EpisodeTitle)
        ? EpisodeKey
        : $"{EpisodeKey}  {EpisodeTitle}";
}
