namespace MediaLibraryNormalizer.Audit;

public sealed record NzbDownloadAttempt(
    string ReleaseKey,
    string ReleaseTitle,
    string? NzbId,
    string? SabNzoId,
    DateTimeOffset AttemptedAt);