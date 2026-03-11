namespace MediaLibraryNormalizer.Config;

/// <summary>
/// Application configuration loaded from normalizer.config.json and/or CLI arguments.
/// </summary>
public class NormalizerConfig
{
    /// <summary>Root library path to scan.</summary>
    public string LibraryPath { get; set; } = string.Empty;

    /// <summary>Fuzzy score threshold for automatic merge (default 92).</summary>
    public int FuzzyThreshold { get; set; } = 92;

    /// <summary>Fuzzy score threshold below which AI is invoked (default 85).</summary>
    public int AiThreshold { get; set; } = 85;

    /// <summary>Size in MB of partial hash blocks (default 1).</summary>
    public int HashSizeMB { get; set; } = 1;

    /// <summary>Max degree of parallelism (default min(8, CPU cores)).</summary>
    public int MaxConcurrency { get; set; } = Math.Min(8, Environment.ProcessorCount);

    /// <summary>Whether to delete sample files.</summary>
    public bool DeleteSamples { get; set; }

    // --- CLI flags ---

    /// <summary>Preview mode — no filesystem changes (default true).</summary>
    public bool DryRun { get; set; } = true;

    /// <summary>Actually apply merge changes.</summary>
    public bool Merge { get; set; }

    /// <summary>
    /// Limit merge planning/execution to exact-match groups where at least one duplicate folder
    /// contains video files. Fuzzy matches and empty-only exact groups are skipped.
    /// </summary>
    public bool ExactMatchesWithFilesOnly { get; set; }

    /// <summary>Enable AI-assisted title resolution.</summary>
    public bool UseAi { get; set; }

    /// <summary>
    /// Delete inferior duplicate episode files when duplicate comparison determines
    /// they should be discarded.
    /// </summary>
    public bool DiscardInferiorDuplicates { get; set; }

    /// <summary>Enable file hashing for duplicate detection.</summary>
    public bool UseHash { get; set; }

    /// <summary>Verbose output.</summary>
    public bool Verbose { get; set; }

    /// <summary>Path to a transaction log for undo.</summary>
    public string? UndoFile { get; set; }

    // --- AI settings ---

    /// <summary>AI API endpoint URL.</summary>
    public string? AiEndpoint { get; set; }

    /// <summary>AI API key (prefer env var NORMALIZER_AI_KEY).</summary>
    public string? AiApiKey { get; set; }

    /// <summary>AI model name.</summary>
    public string? AiModel { get; set; }
}
