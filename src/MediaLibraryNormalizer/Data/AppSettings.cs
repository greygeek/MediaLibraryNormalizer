namespace MediaLibraryNormalizer.Data;

/// <summary>
/// Application user settings persisted to the database.
/// All fields default to sensible values so a missing row is non-fatal.
/// </summary>
public class AppSettings
{
    public string LibraryPath { get; set; } = string.Empty;

    // ── Catalog / MEF settings ───────────────────────────────────────────────

    public string TheTvdbApiKey { get; set; } = string.Empty;

    public string NzbApiKey { get; set; } = string.Empty;

    public string NzbWatchFolder { get; set; } = string.Empty;

    public string NzbCategory { get; set; } = string.Empty;

    public string CatalogProvider { get; set; } = "None";

    public bool IncludeSpecials { get; set; }

    // ── Shared flags ─────────────────────────────────────────────────────────

    public bool Verbose { get; set; }

    // ── Merge Manager settings ───────────────────────────────────────────────

    public bool ExactMatchesWithFilesOnly { get; set; } = true;

    public bool DiscardInferiorDuplicates { get; set; } = true;

    public bool UseAi { get; set; }

    public bool UseHash { get; set; }

    public bool DeleteSamples { get; set; }

    public bool DeleteNonEpisodeFiles { get; set; }

    public bool RenameNonStandardFiles { get; set; }

    public bool FlattenEpisodeReleaseFolders { get; set; }
}
