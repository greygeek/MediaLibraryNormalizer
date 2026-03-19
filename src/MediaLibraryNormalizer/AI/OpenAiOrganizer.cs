using System.Net.Http.Json;
using System.Text.Json;
using MediaLibraryNormalizer.Config;
using Microsoft.Extensions.Logging;

namespace MediaLibraryNormalizer.AI;

/// <summary>
/// AI organizer that sends non-conforming video file paths to an OpenAI-compatible API
/// and converts the response to file move operations.
/// </summary>
public class OpenAiOrganizer(
    HttpClient httpClient,
    NormalizerConfig config,
    ILogger<OpenAiOrganizer> logger) : IAiOrganizer
{
    private const int BatchSize = 40;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<AiMoveInstruction>> SuggestMovesAsync(
        string libraryRoot,
        IReadOnlyList<string> videoFilePaths,
        CancellationToken ct = default)
    {
        var apiKey = config.AiApiKey ?? Environment.GetEnvironmentVariable("NORMALIZER_AI_KEY");
        if (!string.IsNullOrEmpty(apiKey))
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        var results = new List<AiMoveInstruction>();

        for (var i = 0; i < videoFilePaths.Count; i += BatchSize)
        {
            ct.ThrowIfCancellationRequested();
            var batch = videoFilePaths.Skip(i).Take(BatchSize).ToList();
            var batchResults = await ProcessBatchAsync(libraryRoot, batch, ct);
            results.AddRange(batchResults);
        }

        return results;
    }

    private async Task<List<AiMoveInstruction>> ProcessBatchAsync(
        string libraryRoot, List<string> batch, CancellationToken ct)
    {
        var inputJson = JsonSerializer.Serialize(batch);
        var prompt = BuildPrompt(libraryRoot, inputJson);
        var endpoint = config.AiEndpoint ?? "https://api.openai.com/v1/chat/completions";
        var model = config.AiModel ?? "gpt-4o-mini";

        var request = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = "You are a TV library file organizer. Return only valid JSON." },
                new { role = "user", content = prompt }
            },
            temperature = 0.0,
            max_tokens = 4096
        };

        logger.LogDebug("AI organizer request: {Count} files", batch.Count);

        try
        {
            var response = await httpClient.PostAsJsonAsync(endpoint, request, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var content = json
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString()
                ?.Trim();

            if (string.IsNullOrWhiteSpace(content))
                return [];

            // Strip markdown code fences if the model wrapped the response
            if (content.StartsWith("```"))
            {
                var newline = content.IndexOf('\n');
                var closing = content.LastIndexOf("```");
                content = (newline >= 0 && closing > newline)
                    ? content[(newline + 1)..closing].Trim()
                    : content.Trim('`', '\n', ' ');
            }

            var suggestions = JsonSerializer.Deserialize<List<AiMoveInstruction>>(content, JsonOptions);
            if (suggestions is null || suggestions.Count == 0)
                return [];

            // Security: validate every returned path before accepting it
            var inputSet = new HashSet<string>(batch, StringComparer.OrdinalIgnoreCase);
            var normalizedRoot = Path.GetFullPath(libraryRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            var safe = new List<AiMoveInstruction>();
            foreach (var s in suggestions)
            {
                if (string.IsNullOrWhiteSpace(s.Source) || string.IsNullOrWhiteSpace(s.Destination))
                    continue;

                // Source must be one of our inputs (prevents injection)
                if (!inputSet.Contains(s.Source))
                {
                    logger.LogWarning("AI organizer: unexpected source path rejected: {Source}", s.Source);
                    continue;
                }

                // Destination must be inside the library root (prevents path traversal)
                var fullDest = Path.GetFullPath(s.Destination);
                if (!fullDest.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning("AI organizer: destination outside library root rejected: {Dest}", s.Destination);
                    continue;
                }

                safe.Add(s with { Destination = fullDest });
            }

            logger.LogInformation(
                "AI organizer: {Accepted}/{Total} suggestions accepted for batch of {BatchSize}",
                safe.Count, suggestions.Count, batch.Count);

            return safe;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI organizer batch failed");
            return [];
        }
    }

    private static string BuildPrompt(string libraryRoot, string inputFilePathsJson)
    {
        var sep = Path.DirectorySeparatorChar;
        return $$"""
            You are a TV library organizer. The following video files are in non-standard folders and need to be sorted into a proper library structure.
            For each file, identify which TV series and season it belongs to, then return a move operation.

            Library root: {{libraryRoot}}
            Destination format: {{libraryRoot}}{{sep}}{Series Name}{{sep}}Season {N}{{sep}}{original filename}

            Rules:
            - Keep the original filename exactly as-is (do not rename it)
            - Use the canonical series name (e.g. "Brooklyn Nine-Nine", "Black Mirror")
            - Only include entries you are confident about — omit files you cannot identify
            - Return ONLY a JSON array with no markdown, no code fences, no explanation

            Input file paths (JSON array):
            {{inputFilePathsJson}}

            Return format (JSON array only):
            [{"source": "...", "destination": "..."}]
            """;
    }
}
