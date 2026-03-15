namespace MediaLibraryNormalizer.Data;

public interface IAppSettingsRepository
{
    /// <summary>Loads all settings. Returns defaults when no rows exist yet.</summary>
    Task<AppSettings> LoadAsync(CancellationToken ct = default);

    /// <summary>Persists all settings, inserting or updating each key.</summary>
    Task SaveAsync(AppSettings settings, CancellationToken ct = default);
}
