using System.Net.Http;
using MediaLibraryNormalizer.Audit;
using Xunit.Abstractions;

namespace MediaLibraryNormalizer.Tests.Audit;

/// <summary>
/// Live integration tests for <see cref="TheTvdbSeriesCatalogProvider"/>.
/// These tests make real HTTP calls to the TheTVDB v4 API and require
/// the <c>TVDB_API_KEY</c> environment variable to be set.
///
/// Obtain a free API key at https://thetvdb.com → Account → API Keys.
/// Run with:  $env:TVDB_API_KEY = "your-key" ; dotnet test --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public sealed class TheTvdbIntegrationTests(ITestOutputHelper output)
{
    private const string ApiKeyEnvVar = "TVDB_API_KEY";

    [Fact]
    public async Task SearchSeries_ReturnsResults()
    {
        var apiKey = GetApiKeyOrNull();
        if (apiKey is null) return;

        using var sut = BuildProvider(apiKey);

        var results = await sut.SearchSeriesAsync("Breaking Bad");

        output.WriteLine($"Search returned {results.Count} result(s).");
        foreach (var r in results)
            output.WriteLine($"  [{r.SourceId}] {r.Title} ({r.Year})");

        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Title.Contains("Breaking Bad", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetSeries_ReturnsEpisodes()
    {
        var apiKey = GetApiKeyOrNull();
        if (apiKey is null) return;

        using var sut = BuildProvider(apiKey);

        // Search first to get a stable source ID rather than hard-coding one.
        var searchResults = await sut.SearchSeriesAsync("Breaking Bad");
        var candidate = searchResults.FirstOrDefault(r =>
            r.Title.Contains("Breaking Bad", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(candidate);
        output.WriteLine($"Using series: [{candidate.SourceId}] {candidate.Title} ({candidate.Year})");

        var series = await sut.GetSeriesAsync(candidate.SourceId);

        output.WriteLine($"Title   : {series.Title}");
        output.WriteLine($"Year    : {series.Year}");
        output.WriteLine($"Network : {series.Network}");
        output.WriteLine($"Status  : {series.SeriesStatus}");
        output.WriteLine($"Episodes: {series.Episodes.Count}");
        foreach (var ep in series.Episodes.OrderBy(e => e.SeasonNumber).ThenBy(e => e.EpisodeNumber).Take(5))
            output.WriteLine($"  S{ep.SeasonNumber:D2}E{ep.EpisodeNumber:D2}  {ep.Title}");
        if (series.Episodes.Count > 5) output.WriteLine("  ...");

        Assert.True(series.Episodes.Count > 0, "Expected at least one episode.");
        Assert.Equal("Breaking Bad", series.Title, ignoreCase: true);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private string? GetApiKeyOrNull()
    {
        var key = Environment.GetEnvironmentVariable(ApiKeyEnvVar);
        if (string.IsNullOrWhiteSpace(key))
            output.WriteLine($"SKIPPED — set the {ApiKeyEnvVar} environment variable to run this test.");
        return string.IsNullOrWhiteSpace(key) ? null : key;
    }

    private TheTvdbSeriesCatalogProvider BuildProvider(string apiKey)
    {
        var handler = new LoggingHandler(output);
        var httpClient = new HttpClient(handler);
        return new TheTvdbSeriesCatalogProvider(httpClient, apiKey,
            message => output.WriteLine($"[Provider] {message}"));
    }

    /// <summary>
    /// Delegating handler that writes every request URL and response status + body to
    /// <see cref="ITestOutputHelper"/> so you can see exactly what the TVDB API returns.
    /// </summary>
    private sealed class LoggingHandler(ITestOutputHelper output) : DelegatingHandler(new HttpClientHandler())
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            output.WriteLine($"→ {request.Method} {request.RequestUri}");
            if (request.Content is not null)
            {
                var requestBody = await request.Content.ReadAsStringAsync(cancellationToken);
                output.WriteLine($"  Body: {requestBody}");
            }

            var response = await base.SendAsync(request, cancellationToken);

            // Buffer the content so we can read it here AND the caller can read it again.
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            output.WriteLine($"← {(int)response.StatusCode} {response.ReasonPhrase}");
            // Truncate very long responses (e.g. episode lists) for readability.
            output.WriteLine($"  Body: {(responseBody.Length > 500 ? responseBody[..500] + " …[truncated]" : responseBody)}");

            // Replace content with a buffered copy so downstream readers still work.
            response.Content = new StringContent(responseBody,
                System.Text.Encoding.UTF8,
                response.Content.Headers.ContentType?.MediaType ?? "application/json");

            return response;
        }
    }
}
