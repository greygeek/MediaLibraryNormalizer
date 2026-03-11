using MediaLibraryNormalizer.Models;

namespace MediaLibraryNormalizer.Reporting;

/// <summary>
/// Generates scan/merge reports in multiple formats.
/// </summary>
public interface IReportGenerator
{
    /// <summary>Write console summary output.</summary>
    void WriteConsoleSummary(ScanResult result, List<MergeOperation> operations, bool dryRun);

    /// <summary>Write JSON report to file.</summary>
    Task WriteJsonReportAsync(string filePath, ScanResult result, List<MergeOperation> operations, bool dryRun);
}
