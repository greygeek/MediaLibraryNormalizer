using FuzzySharp;
using MediaLibraryNormalizer.Config;
using MediaLibraryNormalizer.Normalization;
using MediaLibraryNormalizer.Parser;
using MediaLibraryNormalizer.Scanner;
using Microsoft.Extensions.DependencyInjection;

namespace MediaLibraryNormalizer.Audit;

public class SeriesAuditRunner(ISeriesCatalogProvider? catalogProvider = null) : ISeriesAuditRunner
{
    private readonly ISeriesCatalogProvider? _catalogProvider = catalogProvider;

    public async Task<SeriesAuditRunResult> RunAsync(
        SeriesAuditOptions options,
        IProgress<string>? progress = null,
        ICatalogCache? catalogCache = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.LibraryPath))
            throw new InvalidOperationException("Library path must be provided.");

        var config = new NormalizerConfig
        {
            LibraryPath = options.LibraryPath,
            Verbose = options.Verbose
        };

        using var serviceProvider = ServiceRegistration.BuildServiceProvider(config);
        var scanner = serviceProvider.GetRequiredService<ILibraryScanner>();
        var normalizer = serviceProvider.GetRequiredService<INameNormalizer>();
        var episodeParser = serviceProvider.GetRequiredService<IEpisodeParser>();
        var provider = CreateProvider(options, progress);

        progress?.Report("Scanning local library inventory...");

        try
        {
            var seriesResults = new List<SeriesAuditItem>();
            var errors = new List<string>();

            foreach (var item in scanner.Scan(options.LibraryPath))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var normalized = normalizer.Normalize(item.OriginalName);
                    var parsedEpisodeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var unparseableFiles = new List<string>();

                    foreach (var videoFile in item.VideoFiles.Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        var parsedEpisode = episodeParser.Parse(videoFile);
                        if (parsedEpisode is null)
                        {
                            unparseableFiles.Add(videoFile);
                            continue;
                        }

                        foreach (var episode in parsedEpisode.Episodes)
                        {
                            parsedEpisodeKeys.Add($"S{parsedEpisode.Season:D2}E{episode:D2}");
                        }
                    }

                    var status = parsedEpisodeKeys.Count == 0
                        ? AuditSeriesStatus.NoParsedEpisodes
                        : unparseableFiles.Count == 0
                            ? AuditSeriesStatus.ReadyForCatalogLookup
                            : AuditSeriesStatus.PartialInventory;

                    var catalogStatus = CatalogLookupStatus.NotRequested;
                    var catalogStatusMessage = options.CatalogProvider == CatalogProviderKind.None
                        ? "Catalog lookup not requested."
                        : "Catalog lookup skipped because no parsed episodes were detected.";
                    string? catalogMatchedTitle = null;
                    int? catalogMatchedYear = null;
                    var catalogEpisodeCount = 0;
                    var missingEpisodeKeys = new List<string>();
                    var missingEpisodes = new List<MissingEpisodeInfo>();
                    var extraEpisodeKeys = new List<string>();
                    string? catalogSummary = null;
                    var catalogGenres = new List<string>();
                    string? catalogNetwork = null;
                    string? catalogSeriesStatus = null;
                    double? catalogRating = null;
                    string? catalogImageUrl = null;

                    if (provider is not null && parsedEpisodeKeys.Count > 0)
                    {
                        progress?.Report($"Searching {provider.DisplayName} for {item.OriginalName}...");

                        try
                        {
                            var catalogResult = await LookupCatalogSeriesAsync(
                                provider,
                                normalizer,
                                normalized.Title,
                                normalized.Year,
                                parsedEpisodeKeys,
                                options,
                                catalogCache,
                                cancellationToken);

                            catalogStatus = catalogResult.Status;
                            catalogStatusMessage = catalogResult.StatusMessage;
                            catalogMatchedTitle = catalogResult.MatchedTitle;
                            catalogMatchedYear = catalogResult.MatchedYear;
                            catalogEpisodeCount = catalogResult.CatalogEpisodeCount;
                            missingEpisodeKeys = catalogResult.MissingEpisodeKeys;
                            missingEpisodes = catalogResult.MissingEpisodes;
                            extraEpisodeKeys = catalogResult.ExtraEpisodeKeys;
                            catalogSummary = catalogResult.Summary;
                            catalogGenres = catalogResult.Genres;
                            catalogNetwork = catalogResult.Network;
                            catalogSeriesStatus = catalogResult.SeriesStatus;
                            catalogRating = catalogResult.Rating;
                            catalogImageUrl = catalogResult.ImageUrl;
                        }
                        catch (Exception ex)
                        {
                            catalogStatus = CatalogLookupStatus.Error;
                            catalogStatusMessage = ex.Message;
                        }
                    }

                    seriesResults.Add(new SeriesAuditItem
                    {
                        OriginalTitle = item.OriginalName,
                        NormalizedTitle = normalized.Title,
                        Year = normalized.Year,
                        TotalVideoFiles = item.VideoFiles.Count,
                        SeasonFolderCount = item.SeasonFolders.Count,
                        ParsedEpisodeCount = parsedEpisodeKeys.Count,
                        UnparseableFileCount = unparseableFiles.Count,
                        Status = status,
                        CatalogProvider = options.CatalogProvider,
                        CatalogStatus = catalogStatus,
                        CatalogStatusMessage = catalogStatusMessage,
                        CatalogMatchedTitle = catalogMatchedTitle,
                        CatalogMatchedYear = catalogMatchedYear,
                        CatalogEpisodeCount = catalogEpisodeCount,
                        CatalogSummary = catalogSummary,
                        CatalogGenres = catalogGenres,
                        CatalogNetwork = catalogNetwork,
                        CatalogSeriesStatus = catalogSeriesStatus,
                        CatalogRating = catalogRating,
                        CatalogImageUrl = catalogImageUrl,
                        MissingEpisodeCount = missingEpisodeKeys.Count,
                        ExtraEpisodeCount = extraEpisodeKeys.Count,
                        EpisodeKeys = parsedEpisodeKeys.OrderBy(static key => key, StringComparer.OrdinalIgnoreCase).ToList(),
                        MissingEpisodeKeys = missingEpisodeKeys,
                        MissingEpisodes = missingEpisodes,
                        ExtraEpisodeKeys = extraEpisodeKeys,
                        UnparseableFiles = unparseableFiles.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).ToList(),
                        FolderPath = item.Path
                    });

                    progress?.Report($"Indexed {item.OriginalName}");
                }
                catch (Exception ex)
                {
                    errors.Add($"{item.OriginalName}: {ex.Message}");
                }
            }

            var orderedSeries = seriesResults
                .OrderBy(result => result.NormalizedTitle, StringComparer.OrdinalIgnoreCase)
                .ThenBy(result => result.Year)
                .ToList();

            return new SeriesAuditRunResult
            {
                LibraryPath = options.LibraryPath,
                Series = orderedSeries,
                Errors = errors,
                Summary = new SeriesAuditSummary
                {
                    SeriesScanned = orderedSeries.Count,
                    ReadySeries = orderedSeries.Count(result => result.Status == AuditSeriesStatus.ReadyForCatalogLookup),
                    PartialSeries = orderedSeries.Count(result => result.Status == AuditSeriesStatus.PartialInventory),
                    NoParsedEpisodeSeries = orderedSeries.Count(result => result.Status == AuditSeriesStatus.NoParsedEpisodes),
                    ParsedEpisodeCount = orderedSeries.Sum(result => result.ParsedEpisodeCount),
                    UnparseableFileCount = orderedSeries.Sum(result => result.UnparseableFileCount),
                    CatalogMatchedSeries = orderedSeries.Count(result => result.CatalogStatus == CatalogLookupStatus.Matched),
                    CatalogAmbiguousSeries = orderedSeries.Count(result => result.CatalogStatus == CatalogLookupStatus.Ambiguous),
                    CatalogErrorSeries = orderedSeries.Count(result => result.CatalogStatus == CatalogLookupStatus.Error),
                    SeriesWithMissingEpisodes = orderedSeries.Count(result => result.MissingEpisodeCount > 0),
                    MissingEpisodeCount = orderedSeries.Sum(result => result.MissingEpisodeCount)
                }
            };
        }
        finally
        {
            if (_catalogProvider is null && provider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    private ISeriesCatalogProvider? CreateProvider(SeriesAuditOptions options, IProgress<string>? progress)
    {
        if (_catalogProvider is not null)
            return _catalogProvider;

        Action<string>? log = progress is not null ? message => progress.Report(message) : null;

        return options.CatalogProvider switch
        {
            CatalogProviderKind.None => null,
            CatalogProviderKind.TvMaze => new TvMazeSeriesCatalogProvider(new HttpClient(), log),
            CatalogProviderKind.TheTvdb when !string.IsNullOrWhiteSpace(options.TheTvdbApiKey)
                => new TheTvdbSeriesCatalogProvider(options.TheTvdbApiKey, log),
            CatalogProviderKind.TheTvdb => throw new InvalidOperationException(
                "TheTVDB catalog provider requires an API key. Get one at thetvdb.com → Account → API Keys."),
            _ => null
        };
    }

    private static async Task<CatalogEvaluationResult> LookupCatalogSeriesAsync(
        ISeriesCatalogProvider provider,
        INameNormalizer normalizer,
        string normalizedTitle,
        int? year,
        HashSet<string> localEpisodeKeys,
        SeriesAuditOptions options,
        ICatalogCache? catalogCache,
        CancellationToken cancellationToken)
    {
        // 1. Check catalog cache before hitting the API
        CatalogSeries? catalogSeries = null;
        if (catalogCache is not null)
            catalogSeries = await catalogCache.GetAsync(
                provider.Kind, normalizedTitle, options.CacheExpiryHours, cancellationToken);

        if (catalogSeries is null)
        {
            // 2. Search the provider
            var candidates = await provider.SearchSeriesAsync(normalizedTitle, cancellationToken);
            if (candidates.Count == 0)
                return CatalogEvaluationResult.NoMatch($"No {provider.DisplayName} match found.");

            var exactMatches = candidates
                .Select(candidate => new
                {
                    Candidate = candidate,
                    CandidateTitle = normalizer.Normalize(candidate.Title).Title
                })
                .Where(result => string.Equals(result.CandidateTitle, normalizedTitle, StringComparison.OrdinalIgnoreCase))
                .Select(result => result.Candidate)
                .ToList();

            if (exactMatches.Count == 0)
            {
                // 3. Fuzzy-match fallback
                if (options.FuzzyMatchThreshold > 0)
                {
                    var fuzzyMatches = candidates
                        .Select(c => new
                        {
                            Candidate = c,
                            Score = Fuzz.Ratio(normalizer.Normalize(c.Title).Title, normalizedTitle)
                        })
                        .Where(x => x.Score >= options.FuzzyMatchThreshold)
                        .OrderByDescending(x => x.Score)
                        .ToList();

                    if (fuzzyMatches.Count == 0)
                        return CatalogEvaluationResult.NoMatch(
                            $"No {provider.DisplayName} match found (best fuzzy score below {options.FuzzyMatchThreshold}%).");

                    exactMatches = fuzzyMatches.Select(static x => x.Candidate).ToList();
                }
                else
                {
                    return CatalogEvaluationResult.NoMatch(
                        $"No exact normalized {provider.DisplayName} title match found.");
                }
            }

            var selectedCandidate = SelectCandidate(exactMatches, year);
            if (selectedCandidate is null)
                return CatalogEvaluationResult.Ambiguous($"Multiple plausible {provider.DisplayName} matches were found.");

            catalogSeries = await provider.GetSeriesAsync(selectedCandidate.SourceId, cancellationToken);

            // 4. Cache the fetched series
            if (catalogCache is not null)
                await catalogCache.SetAsync(provider.Kind, normalizedTitle, catalogSeries, cancellationToken);
        }

        // 5. Compute missing/extra episodes from (possibly cached) catalog data
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var filteredEpisodes = catalogSeries.Episodes
            .Where(e => options.IncludeSpecials || !e.IsSpecial)
            .Where(e => !e.AirDate.HasValue || e.AirDate.Value <= today)
            .ToList();

        var catalogEpisodeKeys = filteredEpisodes
            .Select(static e => e.EpisodeKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var missingEpisodeKeys = catalogEpisodeKeys
            .Except(localEpisodeKeys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var extraEpisodeKeys = localEpisodeKeys
            .Except(catalogEpisodeKeys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var missingKeysSet = new HashSet<string>(missingEpisodeKeys, StringComparer.OrdinalIgnoreCase);
        var missingEpisodes = filteredEpisodes
            .Where(e => missingKeysSet.Contains(e.EpisodeKey))
            .OrderBy(static e => e.EpisodeKey, StringComparer.OrdinalIgnoreCase)
            .Select(static e => new MissingEpisodeInfo(e.EpisodeKey, e.Title, e.AirDate))
            .ToList();

        return CatalogEvaluationResult.Matched(
            catalogSeries.Title,
            catalogSeries.Year,
            catalogEpisodeKeys.Count,
            missingEpisodeKeys,
            missingEpisodes,
            extraEpisodeKeys,
            catalogSeries.Summary,
            catalogSeries.Genres,
            catalogSeries.Network,
            catalogSeries.SeriesStatus,
            catalogSeries.Rating,
            catalogSeries.ImageUrl,
            missingEpisodeKeys.Count == 0
                ? $"Matched against {provider.DisplayName}; no missing aired episodes found."
                : $"Matched against {provider.DisplayName}; {missingEpisodeKeys.Count} aired episode(s) missing.");
    }

    private static CatalogSeriesCandidate? SelectCandidate(List<CatalogSeriesCandidate> candidates, int? year)
    {
        if (year.HasValue)
        {
            var yearMatches = candidates.Where(candidate => candidate.Year == year).ToList();
            if (yearMatches.Count == 1)
                return yearMatches[0];

            if (yearMatches.Count > 1)
                return null;
        }

        return candidates.Count == 1 ? candidates[0] : null;
    }

    private sealed class CatalogEvaluationResult
    {
        public required CatalogLookupStatus Status { get; init; }

        public required string StatusMessage { get; init; }

        public string? MatchedTitle { get; init; }

        public int? MatchedYear { get; init; }

        public int CatalogEpisodeCount { get; init; }

        public string? Summary { get; init; }

        public List<string> Genres { get; init; } = [];

        public string? Network { get; init; }

        public string? SeriesStatus { get; init; }

        public double? Rating { get; init; }

        public string? ImageUrl { get; init; }

        public List<string> MissingEpisodeKeys { get; init; } = [];

        public List<MissingEpisodeInfo> MissingEpisodes { get; init; } = [];

        public List<string> ExtraEpisodeKeys { get; init; } = [];

        public static CatalogEvaluationResult NoMatch(string message) => new()
        {
            Status = CatalogLookupStatus.NoMatch,
            StatusMessage = message
        };

        public static CatalogEvaluationResult Ambiguous(string message) => new()
        {
            Status = CatalogLookupStatus.Ambiguous,
            StatusMessage = message
        };

        public static CatalogEvaluationResult Matched(
            string matchedTitle,
            int? matchedYear,
            int catalogEpisodeCount,
            List<string> missingEpisodeKeys,
            List<MissingEpisodeInfo> missingEpisodes,
            List<string> extraEpisodeKeys,
            string? summary,
            List<string> genres,
            string? network,
            string? seriesStatus,
            double? rating,
            string? imageUrl,
            string message) => new()
            {
                Status = CatalogLookupStatus.Matched,
                StatusMessage = message,
                MatchedTitle = matchedTitle,
                MatchedYear = matchedYear,
                CatalogEpisodeCount = catalogEpisodeCount,
                MissingEpisodeKeys = missingEpisodeKeys,
                MissingEpisodes = missingEpisodes,
                ExtraEpisodeKeys = extraEpisodeKeys,
                Summary = summary,
                Genres = genres,
                Network = network,
                SeriesStatus = seriesStatus,
                Rating = rating,
                ImageUrl = imageUrl
            };
    }
}