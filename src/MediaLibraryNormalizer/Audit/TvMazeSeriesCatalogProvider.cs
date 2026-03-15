using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace MediaLibraryNormalizer.Audit;

public sealed class TvMazeSeriesCatalogProvider(HttpClient httpClient) : ISeriesCatalogProvider, IDisposable
{
    private readonly HttpClient _httpClient = httpClient;

    public CatalogProviderKind Kind => CatalogProviderKind.TvMaze;

    public string DisplayName => "TVMaze";

    public async Task<IReadOnlyList<CatalogSeriesCandidate>> SearchSeriesAsync(string title, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            return [];

        var encodedTitle = Uri.EscapeDataString(title.Trim());
        var response = await _httpClient.GetFromJsonAsync<List<TvMazeSearchResult>>(
            $"https://api.tvmaze.com/search/shows?q={encodedTitle}",
            cancellationToken);

        if (response is null)
            return [];

        return response
            .Where(static result => result.Show is not null && result.Show.Id > 0 && !string.IsNullOrWhiteSpace(result.Show.Name))
            .Select(result => new CatalogSeriesCandidate
            {
                SourceId = result.Show!.Id.ToString(),
                Title = result.Show.Name,
                Year = ParseYear(result.Show.Premiered)
            })
            .ToList();
    }

    public async Task<CatalogSeries> GetSeriesAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        var encodedId = Uri.EscapeDataString(sourceId);
        var show = await _httpClient.GetFromJsonAsync<TvMazeShow>(
            $"https://api.tvmaze.com/shows/{encodedId}",
            cancellationToken)
            ?? throw new InvalidOperationException($"TVMaze show '{sourceId}' was not found.");

        var episodes = await _httpClient.GetFromJsonAsync<List<TvMazeEpisode>>(
            $"https://api.tvmaze.com/shows/{encodedId}/episodes",
            cancellationToken)
            ?? [];

        return new CatalogSeries
        {
            SourceId = sourceId,
            SourceName = DisplayName,
            Title = show.Name,
            Year = ParseYear(show.Premiered),
            Episodes = episodes
                .Where(static episode => episode.Number > 0)
                .Select(static episode => new CatalogEpisode
                {
                    SeasonNumber = episode.Season,
                    EpisodeNumber = episode.Number,
                    Title = episode.Name ?? string.Empty,
                    AirDate = ParseAirDate(episode.AirDate)
                })
                .ToList()
        };
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private static int? ParseYear(string? premiered)
    {
        if (string.IsNullOrWhiteSpace(premiered))
            return null;

        return DateOnly.TryParse(premiered, out var parsed)
            ? parsed.Year
            : null;
    }

    private static DateOnly? ParseAirDate(string? airDate)
    {
        return DateOnly.TryParse(airDate, out var parsed)
            ? parsed
            : null;
    }

    private sealed class TvMazeSearchResult
    {
        [JsonPropertyName("show")]
        public TvMazeShow? Show { get; init; }
    }

    private sealed class TvMazeShow
    {
        [JsonPropertyName("id")]
        public int Id { get; init; }

        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("premiered")]
        public string? Premiered { get; init; }
    }

    private sealed class TvMazeEpisode
    {
        [JsonPropertyName("season")]
        public int Season { get; init; }

        [JsonPropertyName("number")]
        public int Number { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("airdate")]
        public string? AirDate { get; init; }
    }
}