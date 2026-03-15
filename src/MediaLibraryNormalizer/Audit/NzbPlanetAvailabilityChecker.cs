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

    public NzbPlanetAvailabilityChecker(string apiKey, HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("NZBPlanet API key must not be empty.", nameof(apiKey));

        _apiKey = apiKey.Trim();
        _httpClient = httpClient ?? new HttpClient();
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
        using var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        var xml = await response.Content.ReadAsStringAsync(ct);
        return ParseRssItems(xml);
    }

    private string BuildUrl(string title, int season, int episode, string? tvMazeId, string? tvdbId)
    {
        var sb = new System.Text.StringBuilder(256);
        sb.Append(ApiBase);
        sb.Append($"?t=tvsearch&apikey={Uri.EscapeDataString(_apiKey)}");
        sb.Append($"&season={season}&ep={episode}");
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
                var guid = item.Element("guid")?.Value;
                return new NzbSearchResult(title, sizeBytes, postedAt, link, guid);
            }).ToList();
        }
        catch
        {
            return [];
        }
    }

    public async Task<bool> AddToCartAsync(string nzbId, CancellationToken ct = default)
    {
        var url = $"{ApiBase}?t=cart&action=add&id={Uri.EscapeDataString(nzbId)}&apikey={Uri.EscapeDataString(_apiKey)}";
        using var response = await _httpClient.GetAsync(url, ct);
        return response.IsSuccessStatusCode;
    }

    public void Dispose() => _httpClient.Dispose();
}
