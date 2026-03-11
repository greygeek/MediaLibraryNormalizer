using System.Net.Http.Json;
using System.Text.Json;
using MediaLibraryNormalizer.Config;
using MediaLibraryNormalizer.Models;
using Microsoft.Extensions.Logging;

namespace MediaLibraryNormalizer.AI;

/// <summary>
/// AI title resolver using OpenAI-compatible API (OpenAI, Azure OpenAI, local LLM).
/// </summary>
public class OpenAiResolver : IAiResolver
{
    private readonly HttpClient _httpClient;
    private readonly NormalizerConfig _config;
    private readonly ILogger<OpenAiResolver> _logger;

    public OpenAiResolver(HttpClient httpClient, NormalizerConfig config, ILogger<OpenAiResolver> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;

        // Configure API key from config or environment variable
        var apiKey = config.AiApiKey
            ?? Environment.GetEnvironmentVariable("NORMALIZER_AI_KEY");

        if (!string.IsNullOrEmpty(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        }
    }

    public async Task<AiVerdict> ResolveAsync(AiContext context)
    {
        var prompt = BuildPrompt(context);
        var endpoint = _config.AiEndpoint ?? "https://api.openai.com/v1/chat/completions";
        var model = _config.AiModel ?? "gpt-4o-mini";

        var request = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = "You are a television metadata expert." },
                new { role = "user", content = prompt }
            },
            temperature = 0.0,
            max_tokens = 20
        };

        _logger.LogDebug("AI request: {Prompt}", prompt);

        var response = await _httpClient.PostAsJsonAsync(endpoint, request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var content = json
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString()
            ?.Trim()
            .ToUpperInvariant();

        _logger.LogDebug("AI response: {Content}", content);

        return content switch
        {
            "SAME_SERIES" => AiVerdict.SameSeries,
            "DIFFERENT_SERIES" => AiVerdict.DifferentSeries,
            _ => AiVerdict.Uncertain
        };
    }

    private static string BuildPrompt(AiContext context)
    {
        var yearLine = context.DetectedYear.HasValue
            ? $"\nDetected year: {context.DetectedYear}"
            : "";

        return $"""
            Determine whether these titles refer to the same television series.

            Title A: {context.TitleA}
            Title B: {context.TitleB}

            Context:
            Folder A contains {context.FolderAFileCount} files
            Folder B contains {context.FolderBFileCount} files{yearLine}

            Respond with one of:

            SAME_SERIES
            DIFFERENT_SERIES
            UNCERTAIN
            """;
    }
}
