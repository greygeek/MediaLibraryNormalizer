namespace MediaLibraryNormalizer.Data.Entities;

public class NzbDownloadAttemptEntity
{
    public int Id { get; set; }

    public string LibraryPath { get; set; } = string.Empty;

    public string SeriesIdentityKey { get; set; } = string.Empty;

    public string EpisodeKey { get; set; } = string.Empty;

    public string ReleaseKey { get; set; } = string.Empty;

    public string ReleaseTitle { get; set; } = string.Empty;

    public string? NzbId { get; set; }

    public string? SabNzoId { get; set; }

    public string? DownloadUrl { get; set; }

    public string AttemptedAt { get; set; } = string.Empty;
}