using System.Text.Json;

namespace MediaLibraryNormalizer.Config;

/// <summary>
/// Loads configuration from normalizer.config.json file.
/// </summary>
public static class ConfigLoader
{
    private const string ConfigFileName = "normalizer.config.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Load config from the config file in the current directory or next to the executable.
    /// Returns default config if no file is found.
    /// </summary>
    public static NormalizerConfig Load()
    {
        var configPath = FindConfigFile();
        if (configPath is null)
            return new NormalizerConfig();

        var json = File.ReadAllText(configPath);
        return JsonSerializer.Deserialize<NormalizerConfig>(json, JsonOptions)
               ?? new NormalizerConfig();
    }

    private static string? FindConfigFile()
    {
        var candidates = EnumerateSearchDirectories(
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory);

        foreach (var directory in candidates)
        {
            var path = Path.Combine(directory, ConfigFileName);
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    private static IEnumerable<string> EnumerateSearchDirectories(params string[] roots)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots.Where(static path => !string.IsNullOrWhiteSpace(path)))
        {
            var current = new DirectoryInfo(root);
            while (current is not null)
            {
                if (seen.Add(current.FullName))
                    yield return current.FullName;

                current = current.Parent;
            }
        }
    }
}
