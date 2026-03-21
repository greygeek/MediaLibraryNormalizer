using MediaLibraryNormalizer.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MediaLibraryNormalizer.Data;

public sealed class AppSettingsRepository(AppDbContextFactory factory) : IAppSettingsRepository
{
    public async Task<AppSettings> LoadAsync(CancellationToken ct = default)
    {
        await using var db = factory.Create();
        var rows = await db.Settings.ToDictionaryAsync(s => s.Key, s => s.Value, ct);
        return new AppSettings
        {
            LibraryPath = rows.GetValueOrDefault("LibraryPath", string.Empty),
            TheTvdbApiKey = rows.GetValueOrDefault("TheTvdbApiKey", string.Empty),
            NzbApiKey = rows.GetValueOrDefault("NzbApiKey", string.Empty),
            NzbWatchFolder = rows.GetValueOrDefault("NzbWatchFolder", string.Empty),
            NzbCategory = rows.GetValueOrDefault("NzbCategory", string.Empty),
            SabnzbdUrl = rows.GetValueOrDefault("SabnzbdUrl", string.Empty),
            SabnzbdApiKey = rows.GetValueOrDefault("SabnzbdApiKey", string.Empty),
            CatalogProvider = rows.GetValueOrDefault("CatalogProvider", "None"),
            IncludeSpecials = rows.GetValueOrDefault("IncludeSpecials", "false") == "true",
            ExcludeSeasonZeroOnlyMissingSeries = rows.GetValueOrDefault("ExcludeSeasonZeroOnlyMissingSeries", "false") == "true",
            Verbose = rows.GetValueOrDefault("Verbose", "false") == "true",
            ExactMatchesWithFilesOnly = rows.GetValueOrDefault("ExactMatchesWithFilesOnly", "true") == "true",
            DiscardInferiorDuplicates = rows.GetValueOrDefault("DiscardInferiorDuplicates", "true") == "true",
            UseAi = rows.GetValueOrDefault("UseAi", "false") == "true",
            UseHash = rows.GetValueOrDefault("UseHash", "false") == "true",
            DeleteSamples = rows.GetValueOrDefault("DeleteSamples", "false") == "true",
            DeleteNonEpisodeFiles = rows.GetValueOrDefault("DeleteNonEpisodeFiles", "false") == "true",
            RenameNonStandardFiles = rows.GetValueOrDefault("RenameNonStandardFiles", "false") == "true",
            FlattenEpisodeReleaseFolders = rows.GetValueOrDefault("FlattenEpisodeReleaseFolders", "false") == "true",
            UseAiOrganizer = rows.GetValueOrDefault("UseAiOrganizer", "false") == "true",
        };
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        await using var db = factory.Create();

        var pairs = new Dictionary<string, string>
        {
            ["LibraryPath"] = settings.LibraryPath,
            ["TheTvdbApiKey"] = settings.TheTvdbApiKey,
            ["NzbApiKey"] = settings.NzbApiKey,
            ["NzbWatchFolder"] = settings.NzbWatchFolder,
            ["NzbCategory"] = settings.NzbCategory,
            ["SabnzbdUrl"] = settings.SabnzbdUrl,
            ["SabnzbdApiKey"] = settings.SabnzbdApiKey,
            ["CatalogProvider"] = settings.CatalogProvider,
            ["IncludeSpecials"] = settings.IncludeSpecials ? "true" : "false",
            ["ExcludeSeasonZeroOnlyMissingSeries"] = settings.ExcludeSeasonZeroOnlyMissingSeries ? "true" : "false",
            ["Verbose"] = settings.Verbose ? "true" : "false",
            ["ExactMatchesWithFilesOnly"] = settings.ExactMatchesWithFilesOnly ? "true" : "false",
            ["DiscardInferiorDuplicates"] = settings.DiscardInferiorDuplicates ? "true" : "false",
            ["UseAi"] = settings.UseAi ? "true" : "false",
            ["UseHash"] = settings.UseHash ? "true" : "false",
            ["DeleteSamples"] = settings.DeleteSamples ? "true" : "false",
            ["DeleteNonEpisodeFiles"] = settings.DeleteNonEpisodeFiles ? "true" : "false",
            ["RenameNonStandardFiles"] = settings.RenameNonStandardFiles ? "true" : "false",
            ["FlattenEpisodeReleaseFolders"] = settings.FlattenEpisodeReleaseFolders ? "true" : "false",
            ["UseAiOrganizer"] = settings.UseAiOrganizer ? "true" : "false",
        };

        var existingKeys = await db.Settings
            .Where(s => pairs.Keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s, ct);

        foreach (var (key, value) in pairs)
        {
            if (existingKeys.TryGetValue(key, out var row))
                row.Value = value;
            else
                db.Settings.Add(new SettingEntity { Key = key, Value = value });
        }

        await db.SaveChangesAsync(ct);
    }
}
