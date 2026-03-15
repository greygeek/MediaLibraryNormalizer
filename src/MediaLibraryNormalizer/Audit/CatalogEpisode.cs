namespace MediaLibraryNormalizer.Audit;

public class CatalogEpisode
{
    public int SeasonNumber { get; init; }

    public int EpisodeNumber { get; init; }

    public string Title { get; init; } = string.Empty;

    public DateOnly? AirDate { get; init; }

    public bool IsSpecial => SeasonNumber == 0;

    public string EpisodeKey => $"S{SeasonNumber:D2}E{EpisodeNumber:D2}";
}