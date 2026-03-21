using System.Net;
using System.Net.Http;
using MediaLibraryNormalizer.Audit;

namespace MediaLibraryNormalizer.Tests.Audit;

public sealed class SabnzbdHistoryClientTests
{
    [Fact]
    public async Task GetFailedHistoryAsync_ParsesFailedSlots()
    {
        const string json = """
        {
          "history": {
            "slots": [
              {
                "name": "Show.S01E01.1080p.x265",
                "nzb_name": "Show.S01E01.1080p.x265.nzb",
                "status": "Failed",
                "fail_message": "missing articles",
                "category": "TV",
                "duplicate_key": "Show/1/1",
                "nzo_id": "SABnzbd_nzo_1"
              },
              {
                "name": "Show.S01E02.1080p.x264",
                "status": "Completed",
                "category": "TV"
              }
            ]
          }
        }
        """;

        using var client = new SabnzbdHistoryClient(
            "http://localhost:8080",
            "apikey",
            new HttpClient(new StaticResponseHandler(json)));

        var items = await client.GetFailedHistoryAsync("TV");

        var item = Assert.Single(items);
        Assert.Equal("Show.S01E01.1080p.x265", item.Name);
        Assert.Equal("Failed", item.Status);
        Assert.Equal("Show/1/1", item.DuplicateKey);
    }

    [Fact]
    public async Task AddDownloadUrlAsync_ReturnsFirstNzoId()
    {
        const string json = """
        {
          "status": true,
          "nzo_ids": ["SABnzbd_nzo_queued"]
        }
        """;

        using var client = new SabnzbdHistoryClient(
          "http://localhost:8080",
          "apikey",
          new HttpClient(new StaticResponseHandler(json)));

        var result = await client.AddDownloadUrlAsync(
          "https://indexer.invalid/get?id=123",
          "Show.S01E01.1080p.x265",
          "TV");

        Assert.True(result.Success);
        Assert.Equal("SABnzbd_nzo_queued", result.NzoId);
    }

    [Fact]
    public async Task RetryHistoryItemAsync_ReturnsTrueWhenSabAcceptsRetry()
    {
        const string json = """
        {
          "status": true
        }
        """;

        using var client = new SabnzbdHistoryClient(
          "http://localhost:8080",
          "apikey",
          new HttpClient(new StaticResponseHandler(json)));

        var result = await client.RetryHistoryItemAsync("SABnzbd_nzo_failed");

        Assert.True(result);
    }

    private sealed class StaticResponseHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }
}