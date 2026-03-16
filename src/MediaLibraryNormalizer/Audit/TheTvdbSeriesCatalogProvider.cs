using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace MediaLibraryNormalizer.Audit;

/// <summary>
/// Catalog provider backed by TheTVDB v4 API.
/// Authenticates via the TVDB v4 guest-token flow (POST /login with empty apikey),
/// which requires no personal key. The bearer token is cached for the lifetime of this instance.
/// </summary>
public sealed class TheTvdbSeriesCatalogProvider : ISeriesCatalogProvider, IDisposable
{
    private const string BaseUrl = "https://api4.thetvdb.com/v4";

    private readonly HttpClient _httpClient;
    private readonly Action<string>? _log;
    private string? _bearerToken;

    public TheTvdbSeriesCatalogProvider(Action<string>? log = null)
        : this(new HttpClient(), log) { }

    public TheTvdbSeriesCatalogProvider(HttpClient httpClient, Action<string>? log = null)
    {
        _httpClient = httpClient;
        _log = log;
    }

    public CatalogProviderKind Kind => CatalogProviderKind.TheTvdb;

    public string DisplayName => "TheTVDB";

    public async Task<IReadOnlyList<CatalogSeriesCandidate>> SearchSeriesAsync(
        string title, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            return [];

        await EnsureAuthenticatedAsync(cancellationToken);

        var encodedTitle = Uri.EscapeDataString(title.Trim());
        var url = $"{BaseUrl}/search?query={encodedTitle}&type=series";
        var response = await GetWithRetryAsync<TvdbResponse<List<TvdbSearchResult>>>(url, cancellationToken);
        _log?.Invoke($"[TheTVDB] search returned {response?.Data?.Count ?? 0} result(s) for '{title}'");

        if (response?.Data is null)
            return [];

        return response.Data
            .Where(static result => result.TvdbId > 0 && !string.IsNullOrWhiteSpace(result.Name))
            .Select(result => new CatalogSeriesCandidate
            {
                SourceId = result.TvdbId.ToString(),
                Title = result.Name,
                Year = ParseYear(result.Year)
            })
            .ToList();
    }

    public async Task<CatalogSeries> GetSeriesAsync(
        string sourceId, CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken);

        // Fetch series metadata
        var seriesUrl = $"{BaseUrl}/series/{Uri.EscapeDataString(sourceId)}";
        var seriesResponse = await GetWithRetryAsync<TvdbResponse<TvdbSeriesDetail>>(seriesUrl, cancellationToken);
        var seriesDetail = seriesResponse?.Data;

        // Fetch episodes (paginated)
        var allEpisodes = new List<TvdbEpisode>();
        var page = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var url = $"{BaseUrl}/series/{Uri.EscapeDataString(sourceId)}/episodes/default?page={page}";
            var response = await GetWithRetryAsync<TvdbResponse<TvdbEpisodesData>>(url, cancellationToken)
                ?? throw new InvalidOperationException($"TheTVDB series '{sourceId}' was not found.");

            if (response.Data?.Episodes is { Count: > 0 } episodes)
                allEpisodes.AddRange(episodes);

            if (response.Links?.Next is null)
                break;

            page++;
        }

        var title = seriesDetail?.Name
            ?? (allEpisodes.Count > 0 ? allEpisodes[0].SeriesName : null)
            ?? sourceId;

        return new CatalogSeries
        {
            SourceId = sourceId,
            SourceName = DisplayName,
            Title = title,
            Year = ParseYear(seriesDetail?.FirstAired),
            Summary = seriesDetail?.Overview,
            Genres = seriesDetail?.Genres?.Select(static g => g.Name ?? string.Empty)
                .Where(static n => !string.IsNullOrWhiteSpace(n)).ToList() ?? [],
            Network = seriesDetail?.LatestNetwork?.Name,
            SeriesStatus = seriesDetail?.Status?.Name,
            Rating = seriesDetail?.Score,
            ImageUrl = seriesDetail?.Image,
            Episodes = allEpisodes
                .Where(static episode => episode.Number > 0)
                .Select(static episode => new CatalogEpisode
                {
                    SeasonNumber = episode.SeasonNumber,
                    EpisodeNumber = episode.Number,
                    Title = episode.Name ?? string.Empty,
                    AirDate = ParseAirDate(episode.Aired)
                })
                .ToList()
        };
    }

    public void Dispose() => _httpClient.Dispose();

    private async Task<T?> GetWithRetryAsync<T>(string url, CancellationToken ct)
    {
        int[] delaySeconds = [1, 2, 4];
        for (int attempt = 0; ; attempt++)
        {
            _log?.Invoke($"[TheTVDB] GET {url}" + (attempt > 0 ? $" (retry {attempt})" : string.Empty));
            using var response = await _httpClient.GetAsync(url, ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                // Bearer token has expired — clear it and re-authenticate, but only once.
                if (attempt == 0)
                {
                    _log?.Invoke("[TheTVDB] 401 Unauthorized — re-authenticating...");
                    _bearerToken = null;
                    _httpClient.DefaultRequestHeaders.Authorization = null;
                    await EnsureAuthenticatedAsync(ct);
                    continue;
                }
                response.EnsureSuccessStatusCode(); // throws with clear message
            }

            if (response.StatusCode != HttpStatusCode.TooManyRequests || attempt >= delaySeconds.Length)
            {
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
            }
            _log?.Invoke($"[TheTVDB] rate-limited (429); retrying in {delaySeconds[attempt]}s...");
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds[attempt]), ct);
        }
    }

    private async Task EnsureAuthenticatedAsync(CancellationToken cancellationToken)
    {
        if (_bearerToken is not null)
            return;

        var loginUrl = $"{BaseUrl}/login";
        _log?.Invoke($"[TheTVDB] POST {loginUrl} (guest authentication)");

        // TVDB v4 guest-token flow: send empty apikey to obtain a public bearer token.
        var loginBody = new TvdbLoginRequest(ApiKey: string.Empty);
        var loginResponse = await _httpClient.PostAsJsonAsync(
            loginUrl, loginBody, cancellationToken);

        if (!loginResponse.IsSuccessStatusCode)
        {
            var errorBody = await loginResponse.Content.ReadAsStringAsync(cancellationToken);
            _log?.Invoke($"[TheTVDB] Login failed {(int)loginResponse.StatusCode} {loginResponse.ReasonPhrase}: {errorBody}");
            throw new HttpRequestException(
                $"TheTVDB login failed ({(int)loginResponse.StatusCode} {loginResponse.ReasonPhrase}). " +
                $"Check your API key at thetvdb.com → Account → API Keys. Response: {errorBody}");
        }

        var loginResult = await loginResponse.Content
            .ReadFromJsonAsync<TvdbResponse<TvdbLoginData>>(cancellationToken)
            ?? throw new InvalidOperationException("TheTVDB login returned an empty response.");

        var token = loginResult.Data?.Token
            ?? throw new InvalidOperationException("TheTVDB login succeeded but returned no token.");

        _bearerToken = token;
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        _log?.Invoke("[TheTVDB] Authenticated successfully.");
    }

    private static int? ParseYear(string? year)
    {
        if (string.IsNullOrWhiteSpace(year))
            return null;

        return int.TryParse(year, out var parsed) ? parsed : null;
    }

    private static DateOnly? ParseAirDate(string? aired)
    {
        return DateOnly.TryParse(aired, out var parsed) ? parsed : null;
    }

    // --- JSON DTOs ---

    // TVDB v4 guest login — apikey is intentionally empty.
    private sealed record TvdbLoginRequest(
        [property: JsonPropertyName("apikey")] string ApiKey);

    private sealed class TvdbResponse<T>
    {
        [JsonPropertyName("status")]
        public string? Status { get; init; }

        [JsonPropertyName("data")]
        public T? Data { get; init; }

        [JsonPropertyName("links")]
        public TvdbLinks? Links { get; init; }
    }

    private sealed class TvdbLinks
    {
        [JsonPropertyName("next")]
        public string? Next { get; init; }
    }

    private sealed class TvdbLoginData
    {
        [JsonPropertyName("token")]
        public string? Token { get; init; }
    }

    private sealed class TvdbSearchResult
    {
        [JsonPropertyName("tvdb_id")]
        public int TvdbId { get; init; }

        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("year")]
        public string? Year { get; init; }
    }

    private sealed class TvdbEpisodesData
    {
        [JsonPropertyName("episodes")]
        public List<TvdbEpisode>? Episodes { get; init; }
    }

    private sealed class TvdbSeriesDetail
    {
        [JsonPropertyName("id")]
        public int Id { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("firstAired")]
        public string? FirstAired { get; init; }

        [JsonPropertyName("overview")]
        public string? Overview { get; init; }

        [JsonPropertyName("genres")]
        public List<TvdbGenre>? Genres { get; init; }

        [JsonPropertyName("latestNetwork")]
        public TvdbNetwork? LatestNetwork { get; init; }

        [JsonPropertyName("status")]
        public TvdbStatus? Status { get; init; }

        [JsonPropertyName("score")]
        public double? Score { get; init; }

        [JsonPropertyName("image")]
        public string? Image { get; init; }
    }

    private sealed class TvdbGenre
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }
    }

    private sealed class TvdbNetwork
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }
    }

    private sealed class TvdbStatus
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }
    }

    private sealed class TvdbEpisode
    {
        [JsonPropertyName("id")]
        public int Id { get; init; }

        [JsonPropertyName("seriesName")]
        public string? SeriesName { get; init; }

        [JsonPropertyName("seasonNumber")]
        public int SeasonNumber { get; init; }

        [JsonPropertyName("number")]
        public int Number { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("aired")]
        public string? Aired { get; init; }
    }
}
