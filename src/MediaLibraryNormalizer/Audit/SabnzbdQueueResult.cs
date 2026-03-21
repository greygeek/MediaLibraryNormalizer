namespace MediaLibraryNormalizer.Audit;

public sealed record SabnzbdQueueResult(bool Success, string? NzoId, string? ErrorMessage = null);