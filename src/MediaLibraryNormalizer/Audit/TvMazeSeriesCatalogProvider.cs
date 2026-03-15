using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace MediaLibraryNormalizer.Audit;

public sealed class TvMazeSeriesCatalogProvider : ISeriesCatalogProvider, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly Action<string>? _log;

    public TvMazeSeriesCatalogProvider(HttpClient httpClient, Action<string>? log = null)
    {
        _httpClient = httpClient;
        _log = log;
    }

    public CatalogProviderKind Kind => CatalogProviderKind.TvMaze;

    public string DisplayName => "TVMaze";

    public async Task<IReadOnlyList<CatalogSeriesCandidate>> SearchSeriesAsync(string title, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            return [];

        var encodedTitle = Uri.EscapeDataString(title.Trim());
        var searchUrl = $"https://api.tvmaze.com/search/shows?q={encodedTitle}";
        var response = await GetWithRetryAsync<List<TvMazeSearchResult>>(searchUrl, cancellationToken);
        _log?.Invoke($"[TVMaze] search returned {response?.Count ?? 0} result(s) for '{title}'");

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
        var show = await GetWithRetryAsync<TvMazeShow>(
            $"https://api.tvmaze.com/shows/{encodedId}", cancellationToken)
            ?? throw new InvalidOperationException($"TVMaze show '{sourceId}' was not found.");

        var episodes = await GetWithRetryAsync<List<TvMazeEpisode>>(
            $"https://api.tvmaze.com/shows/{encodedId}/episodes", cancellationToken)
            ?? [];
        _log?.Invoke($"[TVMaze] fetched {episodes.Count} episode(s) for '{show.Name}'");

        return new CatalogSeries
        {
            SourceId = sourceId,
            SourceName = DisplayName,
            Title = show.Name,
            Year = ParseYear(show.Premiered),
            Summary = StripHtml(show.Summary),
            Genres = show.Genres ?? [],
            Network = show.Network?.Name ?? show.WebChannel?.Name,
            SeriesStatus = show.Status,
            Rating = show.Rating?.Average,
            ImageUrl = show.Image?.Medium ?? show.Image?.Original,
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

    private async Task<T?> GetWithRetryAsync<T>(string url, CancellationToken ct)
    {
        int[] delaySeconds = [1, 2, 4];
        for (int attempt = 0; ; attempt++)
        {
            _log?.Invoke($"[TVMaze] GET {url}" + (attempt > 0 ? $" (retry {attempt})" : string.Empty));
            using var response = await _httpClient.GetAsync(url, ct);
            if (response.StatusCode != HttpStatusCode.TooManyRequests || attempt >= delaySeconds.Length)
            {
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
            }
            _log?.Invoke($"[TVMaze] rate-limited (429); retrying in {delaySeconds[attempt]}s...");
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds[attempt]), ct);
        }
    }

    private static string? StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;
        // Remove all HTML tags with a simple regex-free approach
        var sb = new System.Text.StringBuilder();
        var inTag = false;
        foreach (var ch in html)
        {
            if (ch == '<') { inTag = true; continue; }
            if (ch == '>') { inTag = false; continue; }
            if (!inTag) sb.Append(ch);
        }
        return System.Net.WebUtility.HtmlDecode(sb.ToString().Trim());
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

        [JsonPropertyName("summary")]
        public string? Summary { get; init; }

        [JsonPropertyName("genres")]
        public List<string>? Genres { get; init; }

        [JsonPropertyName("network")]
        public TvMazeNetwork? Network { get; init; }

        [JsonPropertyName("webChannel")]
        public TvMazeNetwork? WebChannel { get; init; }

        [JsonPropertyName("status")]
        public string? Status { get; init; }

        [JsonPropertyName("rating")]
        public TvMazeRating? Rating { get; init; }

        [JsonPropertyName("image")]
        public TvMazeImage? Image { get; init; }
    }

    private sealed class TvMazeNetwork
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }
    }

    private sealed class TvMazeRating
    {
        [JsonPropertyName("average")]
        public double? Average { get; init; }
    }

    private sealed class TvMazeImage
    {
        [JsonPropertyName("medium")]
        public string? Medium { get; init; }

        [JsonPropertyName("original")]
        public string? Original { get; init; }
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