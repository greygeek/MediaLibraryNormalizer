namespace MediaLibraryNormalizer.Desktop.ViewModels;

/// <summary>Represents the NZB availability check result for a single missing episode.</summary>
public class NzbEpisodeResultViewModel
{
    public NzbEpisodeResultViewModel(string episodeKey, string episodeTitle, int nzbCount, bool hasH265)
    {
        EpisodeKey = episodeKey;
        EpisodeTitle = episodeTitle;
        NzbCount = nzbCount;
        HasH265 = hasH265;
    }

    public string EpisodeKey { get; }
    public string EpisodeTitle { get; }
    public int NzbCount { get; }
    public bool HasH265 { get; }

    public string StatusText => NzbCount > 0
        ? HasH265
            ? $"✓  {NzbCount} NZB{(NzbCount == 1 ? string.Empty : "s")} (H265)"
            : $"✓  {NzbCount} NZB{(NzbCount == 1 ? string.Empty : "s")}"
        : "✗  Not found";

    public string DisplayLabel => string.IsNullOrWhiteSpace(EpisodeTitle)
        ? EpisodeKey
        : $"{EpisodeKey}  {EpisodeTitle}";
}
