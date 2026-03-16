namespace MediaLibraryNormalizer.Audit;

/// <summary>
/// Progress update emitted by <see cref="ISeriesAuditRunner.RunAsync"/>.
/// When <see cref="Total"/> is 0 the phase is indeterminate (file-system scan).
/// When <see cref="Total"/> is > 0 the phase is deterministic (catalog lookups).
/// </summary>
public readonly record struct AuditProgressReport(string Message, int Current = 0, int Total = 0);
