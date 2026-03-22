using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace MediaLibraryNormalizer.Audit;

public sealed class SabnzbdHistoryClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _apiKey;
    private readonly Action<string>? _log;

    public SabnzbdHistoryClient(
        string baseUrl,
        string apiKey,
        HttpClient? httpClient = null,
        Action<string>? log = null)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("SABnzbd URL must not be empty.", nameof(baseUrl));
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("SABnzbd API key must not be empty.", nameof(apiKey));

        _baseUrl = baseUrl.Trim().TrimEnd('/');
        _apiKey = apiKey.Trim();
        _httpClient = httpClient ?? new HttpClient();
        _log = log;
    }

    public async Task<IReadOnlyList<SabnzbdHistoryItem>> GetFailedHistoryAsync(
        string? category = null,
        int limit = 500,
        CancellationToken ct = default)
    {
        var url = BuildHistoryUrl(category, limit);
        _log?.Invoke($"[SABnzbd] GET {url.Replace(_apiKey, "***")}");

        using var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<SabHistoryEnvelope>(cancellationToken: ct)
            ?? throw new InvalidOperationException("SABnzbd history API returned an empty response.");

        if (!string.IsNullOrWhiteSpace(payload.Error))
            throw new InvalidOperationException($"SABnzbd API error: {payload.Error}");

        return payload.History?.Slots?
            .Where(static slot => string.Equals(slot.Status, "Failed", StringComparison.OrdinalIgnoreCase))
            .Select(static slot => new SabnzbdHistoryItem(
                slot.Name ?? string.Empty,
                slot.NzbName,
                slot.Status ?? string.Empty,
                slot.FailMessage,
                slot.Category,
                slot.DuplicateKey,
                slot.NzoId))
            .ToList()
            ?? [];
    }

    public async Task<SabnzbdQueueResult> AddDownloadUrlAsync(
        string downloadUrl,
        string jobName,
        string? category = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(downloadUrl))
            throw new ArgumentException("Download URL must not be empty.", nameof(downloadUrl));

        var url = BuildAddUrlUrl(downloadUrl, jobName, category);
        _log?.Invoke($"[SABnzbd] GET {url.Replace(_apiKey, "***")}");

        using var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<SabAddUrlEnvelope>(cancellationToken: ct)
            ?? throw new InvalidOperationException("SABnzbd addurl API returned an empty response.");

        if (!string.IsNullOrWhiteSpace(payload.Error))
            return new SabnzbdQueueResult(false, null, payload.Error);

        if (!payload.Status)
            return new SabnzbdQueueResult(false, null, "SABnzbd addurl returned an unsuccessful status.");

        return new SabnzbdQueueResult(true, payload.NzoIds?.FirstOrDefault());
    }

    public async Task<List<string>> GetQueuedNzoIdsAsync(
        string? category = null,
        CancellationToken ct = default)
    {
        var url = BuildQueueUrl(category);
        _log?.Invoke($"[SABnzbd] GET {url.Replace(_apiKey, "***")}");

        using var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<SabQueueEnvelope>(cancellationToken: ct)
            ?? throw new InvalidOperationException("SABnzbd queue API returned an empty response.");

        if (!string.IsNullOrWhiteSpace(payload.Error))
            throw new InvalidOperationException($"SABnzbd API error: {payload.Error}");

        var queuedNzoIds = new List<string>();
        var slots = payload.Queue?.Slots;
        if (slots is null)
            return queuedNzoIds;

        foreach (var slot in slots)
        {
            var nzoId = slot.NzoId;
            if (!string.IsNullOrWhiteSpace(nzoId))
                queuedNzoIds.Add(nzoId);
        }

        return queuedNzoIds;
    }

    public async Task<bool> RetryHistoryItemAsync(string nzoId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nzoId))
            throw new ArgumentException("SABnzbd nzo_id must not be empty.", nameof(nzoId));

        var url = BuildRetryUrl(nzoId);
        _log?.Invoke($"[SABnzbd] GET {url.Replace(_apiKey, "***")}");

        using var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<SabStatusEnvelope>(cancellationToken: ct)
            ?? throw new InvalidOperationException("SABnzbd retry API returned an empty response.");

        if (!string.IsNullOrWhiteSpace(payload.Error))
            throw new InvalidOperationException($"SABnzbd API error: {payload.Error}");

        return payload.Status;
    }

    private string BuildHistoryUrl(string? category, int limit)
    {
        var builder = new UriBuilder(_baseUrl);
        builder.Path = builder.Path.TrimEnd('/') + "/api";

        var query = System.Web.HttpUtility.ParseQueryString(string.Empty);
        query["mode"] = "history";
        query["output"] = "json";
        query["apikey"] = _apiKey;
        query["failed_only"] = "1";
        query["limit"] = limit.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(category))
            query["cat"] = category.Trim();

        builder.Query = query.ToString() ?? string.Empty;
        return builder.Uri.ToString();
    }

    private string BuildAddUrlUrl(string downloadUrl, string jobName, string? category)
    {
        var builder = new UriBuilder(_baseUrl);
        builder.Path = builder.Path.TrimEnd('/') + "/api";

        var query = System.Web.HttpUtility.ParseQueryString(string.Empty);
        query["mode"] = "addurl";
        query["output"] = "json";
        query["apikey"] = _apiKey;
        query["name"] = downloadUrl;
        query["nzbname"] = jobName;
        query["cat"] = string.IsNullOrWhiteSpace(category) ? "*" : category.Trim();

        builder.Query = query.ToString() ?? string.Empty;
        return builder.Uri.ToString();
    }

    private string BuildQueueUrl(string? category)
    {
        var builder = new UriBuilder(_baseUrl);
        builder.Path = builder.Path.TrimEnd('/') + "/api";

        var query = System.Web.HttpUtility.ParseQueryString(string.Empty);
        query["mode"] = "queue";
        query["output"] = "json";
        query["apikey"] = _apiKey;
        if (!string.IsNullOrWhiteSpace(category))
            query["cat"] = category.Trim();

        builder.Query = query.ToString() ?? string.Empty;
        return builder.Uri.ToString();
    }

    private string BuildRetryUrl(string nzoId)
    {
        var builder = new UriBuilder(_baseUrl);
        builder.Path = builder.Path.TrimEnd('/') + "/api";

        var query = System.Web.HttpUtility.ParseQueryString(string.Empty);
        query["mode"] = "retry";
        query["output"] = "json";
        query["apikey"] = _apiKey;
        query["value"] = nzoId;

        builder.Query = query.ToString() ?? string.Empty;
        return builder.Uri.ToString();
    }

    public void Dispose() => _httpClient.Dispose();

    private sealed class SabHistoryEnvelope
    {
        [JsonPropertyName("history")]
        public SabHistoryPayload? History { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }
    }

    private sealed class SabHistoryPayload
    {
        [JsonPropertyName("slots")]
        public List<SabHistorySlot>? Slots { get; init; }
    }

    private sealed class SabAddUrlEnvelope
    {
        [JsonPropertyName("status")]
        public bool Status { get; init; }

        [JsonPropertyName("nzo_ids")]
        public List<string>? NzoIds { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }
    }

    private sealed class SabStatusEnvelope
    {
        [JsonPropertyName("status")]
        public bool Status { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }
    }

    private sealed class SabQueueEnvelope
    {
        [JsonPropertyName("queue")]
        public SabQueuePayload? Queue { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }
    }

    private sealed class SabQueuePayload
    {
        [JsonPropertyName("slots")]
        public List<SabQueueSlot>? Slots { get; init; }
    }

    private sealed class SabQueueSlot
    {
        [JsonPropertyName("nzo_id")]
        public string? NzoId { get; init; }
    }

    private sealed class SabHistorySlot
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("nzb_name")]
        public string? NzbName { get; init; }

        [JsonPropertyName("status")]
        public string? Status { get; init; }

        [JsonPropertyName("fail_message")]
        public string? FailMessage { get; init; }

        [JsonPropertyName("category")]
        public string? Category { get; init; }

        [JsonPropertyName("duplicate_key")]
        public string? DuplicateKey { get; init; }

        [JsonPropertyName("nzo_id")]
        public string? NzoId { get; init; }
    }
}