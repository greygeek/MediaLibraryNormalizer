using MediaLibraryNormalizer.Models;
using Microsoft.Extensions.Logging;

namespace MediaLibraryNormalizer.Merging;

/// <summary>
/// Reverses operations from a transaction log file.
/// </summary>
public class UndoService(ITransactionLog transactionLog, ILogger<UndoService> logger)
{
    public async Task UndoAsync(string transactionLogPath)
    {
        var operations = await transactionLog.LoadAsync(transactionLogPath);
        logger.LogInformation("Undoing {Count} operations from {Path}", operations.Count, transactionLogPath);

        // Reverse order — undo last operation first
        operations.Reverse();

        foreach (var op in operations)
        {
            if (op.DryRun)
            {
                logger.LogDebug("Skipping dry-run operation: {Type} {Source}", op.Type, op.Source);
                continue;
            }

            try
            {
                switch (op.Type)
                {
                    case OperationType.Move:
                        if (File.Exists(op.Destination))
                        {
                            var dir = Path.GetDirectoryName(op.Source);
                            if (dir is not null && !Directory.Exists(dir))
                                Directory.CreateDirectory(dir);

                            File.Move(op.Destination, op.Source);
                            logger.LogInformation("Undo move: {Dest} → {Source}",
                                op.Destination, op.Source);
                        }
                        break;

                    case OperationType.Delete:
                        if (!Directory.Exists(op.Source))
                        {
                            Directory.CreateDirectory(op.Source);
                            logger.LogInformation("Undo delete: recreated {Path}", op.Source);
                        }
                        break;

                    case OperationType.CreateDirectory:
                        if (Directory.Exists(op.Source) && !Directory.EnumerateFileSystemEntries(op.Source).Any())
                        {
                            Directory.Delete(op.Source);
                            logger.LogInformation("Undo create directory: removed {Path}", op.Source);
                        }
                        break;

                    case OperationType.Rename:
                        if (Directory.Exists(op.Destination))
                        {
                            Directory.Move(op.Destination, op.Source);
                            logger.LogInformation("Undo rename: {Dest} → {Source}",
                                op.Destination, op.Source);
                        }
                        break;

                    case OperationType.DeleteSample:
                    case OperationType.DeleteDuplicate:
                        logger.LogWarning("Cannot undo sample file deletion: {Path}", op.Source);
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Undo failed for {Type} {Source}", op.Type, op.Source);
            }
        }

        logger.LogInformation("Undo complete");
    }
}
