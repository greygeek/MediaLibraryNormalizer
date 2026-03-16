using System.Xml.Linq;

namespace MediaLibraryNormalizer.Audit;

/// <summary>
/// Checks NZBPlanet for Usenet availability of specific TV episodes using the Newznab tvsearch API.
/// </summary>
public sealed class NzbPlanetAvailabilityChecker : INzbAvailabilityChecker, IDisposable
{
    private const string ApiBase = "https://api.nzbplanet.net/api";

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly Action<string>? _log;

    public NzbPlanetAvailabilityChecker(string apiKey, HttpClient? httpClient = null, Action<string>? log = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("NZBPlanet API key must not be empty.", nameof(apiKey));

        _apiKey = apiKey.Trim();
        _httpClient = httpClient ?? new HttpClient();
        _log = log;
    }

    public async Task<IReadOnlyList<NzbSearchResult>> SearchAsync(
        string seriesTitle,
        int season,
        int episode,
        string? tvMazeId = null,
        string? tvdbId = null,
        CancellationToken ct = default)
    {
        var url = BuildUrl(seriesTitle, season, episode, tvMazeId, tvdbId);
        _log?.Invoke($"[NZBPlanet] GET {url.Replace(_apiKey, "***")}");
        using var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        var xml = await response.Content.ReadAsStringAsync(ct);
        var results = ParseRssItems(xml);
        _log?.Invoke($"[NZBPlanet] {results.Count} result(s): " +
            string.Join(", ", results.Select(static r => $"{r.Title} ({r.SizeBytes / 1024 / 1024} MB, id={r.NzbId})")));
        return results;
    }

    private string BuildUrl(string title, int season, int episode, string? tvMazeId, string? tvdbId)
    {
        var sb = new System.Text.StringBuilder(256);
        sb.Append(ApiBase);
        sb.Append($"?t=tvsearch&apikey={Uri.EscapeDataString(_apiKey)}");
        sb.Append($"&season={season}&ep={episode}");
        // Size range: 600 MB – 1.2 GB (Newznab uses MB)
        sb.Append("&minsize=600&maxsize=1229");
        if (!string.IsNullOrWhiteSpace(tvMazeId))
            sb.Append($"&tvmazeid={Uri.EscapeDataString(tvMazeId)}");
        else if (!string.IsNullOrWhiteSpace(tvdbId))
            sb.Append($"&tvdbid={Uri.EscapeDataString(tvdbId)}");
        else
            sb.Append($"&q={Uri.EscapeDataString(title)}");
        return sb.ToString();
    }

    private static IReadOnlyList<NzbSearchResult> ParseRssItems(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            return doc.Descendants("item").Select(item =>
            {
                var title = item.Element("title")?.Value ?? string.Empty;
                // NZB download link is in <enclosure url="..."> or <link>
                var link = item.Element("enclosure")?.Attribute("url")?.Value
                           ?? item.Element("link")?.Value;
                var pubDateStr = item.Element("pubDate")?.Value;
                var postedAt = pubDateStr is not null
                    && DateTimeOffset.TryParse(pubDateStr, out var dt)
                    ? dt : DateTimeOffset.MinValue;
                var sizeStr = item.Element("enclosure")?.Attribute("length")?.Value;
                long sizeBytes = sizeStr is not null && long.TryParse(sizeStr, out var sz) ? sz : 0L;

                // <guid> is often a full URL like https://api.nzbplanet.net/api?t=get&id=12345&apikey=...
                // Extract just the numeric id query parameter for use with t=cart.
                var guidRaw = item.Element("guid")?.Value;
                var nzbId = ExtractIdFromGuid(guidRaw);

                return new NzbSearchResult(title, sizeBytes, postedAt, link, nzbId);
            }).ToList();
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Extracts the numeric/alphanumeric id from a Newznab guid, which may be:
    ///   - A full URL: https://api.nzbplanet.net/api?t=get&amp;id=12345&amp;apikey=...
    ///   - A plain id string: 12345
    /// </summary>
    private static string? ExtractIdFromGuid(string? guid)
    {
        if (string.IsNullOrWhiteSpace(guid)) return null;
        if (!guid.Contains('?') && !guid.Contains('/')) return guid; // already a plain id
        if (Uri.TryCreate(guid, UriKind.Absolute, out var uri))
        {
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var id = query["id"];
            if (!string.IsNullOrWhiteSpace(id)) return id;
        }
        return guid; // fall back to raw value
    }

    public async Task<bool> AddToCartAsync(string nzbId, CancellationToken ct = default)
    {
        var url = $"{ApiBase}?t=cart&action=add&id={Uri.EscapeDataString(nzbId)}&apikey={Uri.EscapeDataString(_apiKey)}";
        _log?.Invoke($"[NZBPlanet] AddToCart GET {url.Replace(_apiKey, "***")}");
        using var response = await _httpClient.GetAsync(url, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        _log?.Invoke($"[NZBPlanet] AddToCart response {(int)response.StatusCode}: {body}");
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Picks the best result from a search response: prefers H265/x265/HEVC releases,
    /// falls back to the first result if none match.
    /// </summary>
    public static NzbSearchResult? SelectPreferred(IReadOnlyList<NzbSearchResult> results)
    {
        if (results.Count == 0) return null;
        return results.FirstOrDefault(static r => IsH265(r.Title)) ?? results[0];
    }

    public static bool HasH265(IReadOnlyList<NzbSearchResult> results) =>
        results.Any(static r => IsH265(r.Title));

    private static bool IsH265(string title) =>
        title.Contains("x265", StringComparison.OrdinalIgnoreCase) ||
        title.Contains("h265", StringComparison.OrdinalIgnoreCase) ||
        title.Contains("h.265", StringComparison.OrdinalIgnoreCase) ||
        title.Contains("hevc", StringComparison.OrdinalIgnoreCase);

    public void Dispose() => _httpClient.Dispose();
}
