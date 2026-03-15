namespace MediaLibraryNormalizer.Data.Entities;

public class CatalogCacheEntity
{
    public int Id { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string LookupKey { get; set; } = string.Empty;

    public string CachedAt { get; set; } = string.Empty;

    public string SeriesJson { get; set; } = string.Empty;
}
