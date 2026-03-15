namespace MediaLibraryNormalizer.Audit;

public enum CatalogLookupStatus
{
    NotRequested,
    Matched,
    NoMatch,
    Ambiguous,
    Error
}