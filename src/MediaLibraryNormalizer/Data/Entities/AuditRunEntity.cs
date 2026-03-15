namespace MediaLibraryNormalizer.Data.Entities;

public class AuditRunEntity
{
    public int Id { get; set; }

    public string RunDate { get; set; } = string.Empty;

    public string LibraryPath { get; set; } = string.Empty;

    public string ResultJson { get; set; } = string.Empty;
}
