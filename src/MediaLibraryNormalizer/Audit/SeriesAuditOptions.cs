namespace MediaLibraryNormalizer.Audit;

public class SeriesAuditOptions
{
    public string LibraryPath { get; set; } = string.Empty;

    public bool Verbose { get; set; }

    public bool IncludeSpecials { get; set; }

    public CatalogProviderKind CatalogProvider { get; set; }
}