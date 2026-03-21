namespace MediaLibraryNormalizer.Audit;

public sealed record SabnzbdHistoryItem(
    string Name,
    string? NzbName,
    string Status,
    string? FailMessage,
    string? Category,
    string? DuplicateKey,
    string? NzoId);