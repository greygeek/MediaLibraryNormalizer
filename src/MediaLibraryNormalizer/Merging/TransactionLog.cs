using System.Text.Json;
using MediaLibraryNormalizer.Models;
using Microsoft.Extensions.Logging;

namespace MediaLibraryNormalizer.Merging;

/// <summary>
/// JSON-based transaction log for all filesystem operations.
/// Supports writing and loading for undo.
/// </summary>
public class TransactionLog(ILogger<TransactionLog> logger) : ITransactionLog
{
    private readonly List<MergeOperation> _operations = [];
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public IReadOnlyList<MergeOperation> Operations => _operations.AsReadOnly();

    public Task LogAsync(MergeOperation operation)
    {
        _operations.Add(operation);
        logger.LogDebug("Transaction: {Type} {Source} → {Dest}",
            operation.Type, operation.Source, operation.Destination);
        return Task.CompletedTask;
    }

    public async Task SaveAsync(string filePath)
    {
        var json = JsonSerializer.Serialize(_operations, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
        logger.LogInformation("Transaction log saved: {Path} ({Count} operations)",
            filePath, _operations.Count);
    }

    public async Task<List<MergeOperation>> LoadAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<List<MergeOperation>>(json, JsonOptions) ?? [];
    }
}
