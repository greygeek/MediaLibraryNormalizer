namespace MediaLibraryNormalizer.Desktop.ViewModels;

public class ReviewIssueViewModel
{
    public required string SeriesKey { get; init; }

    public required string GroupName { get; init; }

    public required string FolderPath { get; init; }

    public required string ExceptionType { get; init; }

    public required string Message { get; init; }

    public string Summary => $"{ExceptionType}: {Message}";
}
