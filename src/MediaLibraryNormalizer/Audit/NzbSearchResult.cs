namespace MediaLibraryNormalizer.Audit;

public record NzbSearchResult(string Title, long SizeBytes, DateTimeOffset PostedAt, string? DownloadUrl);
