using MediaLibraryNormalizer.Models;

namespace MediaLibraryNormalizer.Desktop.ViewModels;

public class OperationItemViewModel
{
    public OperationItemViewModel(MergeOperation operation)
    {
        Type = operation.Type.ToString();
        Source = operation.Source;
        Destination = operation.Destination;
        Summary = string.IsNullOrWhiteSpace(operation.Destination)
            ? $"{Type}: {operation.Source}"
            : $"{Type}: {operation.Source} → {operation.Destination}";
    }

    public string Type { get; }

    public string Source { get; }

    public string Destination { get; }

    public string Summary { get; }
}
