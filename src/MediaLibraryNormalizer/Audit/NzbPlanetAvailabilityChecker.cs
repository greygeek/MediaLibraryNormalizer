using System.Xml.Linq;

namespace MediaLibraryNormalizer.Audit;

/// <summary>
/// Checks NZBPlanet for Usenet availability of specific TV episodes using the Newznab tvsearch API.
/// </summary>
public sealed class NzbPlanetAvailabilityChecker : INzbAvailabilityChecker, IDisposable
{
    private const string ApiBase = "https://api.nzbplanet.net/api";
    // User functions (cart) use the /rss endpoint, not /api
    private const string RssBase = "https://api.nzbplanet.net/rss";

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
        // Log first RSS item's raw XML so we can see all available fields/attributes.
        var firstItemXml = System.Text.RegularExpressions.Regex.Match(xml, @"<item>.*?</item>", System.Text.RegularExpressions.RegexOptions.Singleline).Value;
        if (!string.IsNullOrEmpty(firstItemXml))
            _log?.Invoke($"[NZBPlanet] First item XML:{Environment.NewLine}{firstItemXml}");
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

                // Extract the NZB id: prefer the enclosure/download URL (e.g. ?t=get&id=HASH&apikey=...)
                // because it reliably contains the id as a query parameter.
                // Fall back to parsing the <guid> which may be a /details/<hash> URL.
                var guidRaw = item.Element("guid")?.Value;
                var nzbId = ExtractIdFromUrl(link) ?? ExtractIdFromUrl(guidRaw) ?? ExtractPathSegmentId(guidRaw);
                var userId = ExtractParamFromGetnzbUrl(link, "i");
                var rssKey = ExtractParamFromGetnzbUrl(link, "r");

                return new NzbSearchResult(title, sizeBytes, postedAt, link, nzbId, userId, rssKey);
            }).ToList();
        }
        catch
        {
            return [];
        }
    }

    /// Extracts a named parameter from a NZBPlanet getnzb URL (e.g. .../getnzb/HASH.nzb&i=12345&r=KEY).
    private static string? ExtractParamFromGetnzbUrl(string? url, string name)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var m = System.Text.RegularExpressions.Regex.Match(url, $@"[?&]{System.Text.RegularExpressions.Regex.Escape(name)}=([^&]+)");
        return m.Success ? m.Groups[1].Value : null;
    }

    /// Extracts the `id` query parameter from a URL (e.g. ?t=get&id=HASH&apikey=...).
    private static string? ExtractIdFromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (!url.Contains('?')) return null;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        var id = System.Web.HttpUtility.ParseQueryString(uri.Query)["id"];
        return string.IsNullOrWhiteSpace(id) ? null : id;
    }

    /// Extracts the last path segment from a URL (e.g. https://api.nzbplanet.net/details/HASH → HASH).
    private static string? ExtractPathSegmentId(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (!url.Contains('/')) return url; // already a plain id
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        var segment = uri.Segments.LastOrDefault()?.Trim('/');
        return string.IsNullOrWhiteSpace(segment) ? null : segment;
    }

    public async Task<bool> AddToCartAsync(string nzbId, string? userId = null, string? rssKey = null, CancellationToken ct = default)
    {
        // NZBPlanet user functions require i= (user id) and r= (rss/api key) auth.
        // Extract both from the getnzb download URL embedded in every search result —
        // this is the only reliable source, since the RSS key may differ from apikey=.
        string url;
        string logUrl;
        if (!string.IsNullOrWhiteSpace(userId) && !string.IsNullOrWhiteSpace(rssKey))
        {
            url = $"{RssBase}?t=cartadd&id={Uri.EscapeDataString(nzbId)}&i={Uri.EscapeDataString(userId)}&r={Uri.EscapeDataString(rssKey)}";
            logUrl = url.Replace(rssKey, "***");
        }
        else
        {
            // Fallback: use apikey= (may not work for cart; logged so the user can investigate)
            url = $"{RssBase}?t=cartadd&id={Uri.EscapeDataString(nzbId)}&apikey={Uri.EscapeDataString(_apiKey)}";
            logUrl = url.Replace(_apiKey, "***");
            _log?.Invoke("[NZBPlanet] Warning: could not extract i=/r= from download URL; falling back to apikey= for cartadd.");
        }
        _log?.Invoke($"[NZBPlanet] CartAdd GET {logUrl}");
        using var response = await _httpClient.GetAsync(url, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        _log?.Invoke($"[NZBPlanet] CartAdd response {(int)response.StatusCode}: {body}");
        if (!response.IsSuccessStatusCode) return false;
        return !IsNewznabError(body);
    }

    private static bool IsNewznabError(string xmlBody)
    {
        try
        {
            var doc = XDocument.Parse(xmlBody);
            return doc.Root?.Name.LocalName == "error" ||
                   doc.Descendants("error").Any();
        }
        catch { return false; }
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
