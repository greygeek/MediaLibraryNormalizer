namespace MediaLibraryNormalizer.Audit;

public class SeriesAuditOptions
{
    public string LibraryPath { get; set; } = string.Empty;

    public bool Verbose { get; set; }

    public bool IncludeSpecials { get; set; }

    public CatalogProviderKind CatalogProvider { get; set; }

    /// <summary>TheTVDB personal API key (required when CatalogProvider = TheTvdb).</summary>
    public string? TheTvdbApiKey { get; set; }

    /// <summary>Fuzzy title-match score threshold (0–100). Set to 0 to disable fuzzy matching. Default 80.</summary>
    public int FuzzyMatchThreshold { get; set; } = 80;

    /// <summary>How many hours catalog lookup results are cached before a fresh API call is made. Default 168 (7 days).</summary>
    public int CacheExpiryHours { get; set; } = 168;

    /// <summary>NZBPlanet API key. When set, Usenet availability checks are enabled.</summary>
    public string? NzbApiKey { get; set; }
}