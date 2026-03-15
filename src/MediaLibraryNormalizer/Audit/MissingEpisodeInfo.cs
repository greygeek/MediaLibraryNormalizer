namespace MediaLibraryNormalizer.Audit;

/// <summary>An episode that exists in the catalog but is absent from the local library.</summary>
public record MissingEpisodeInfo(string Key, string Title, DateOnly? AirDate);
