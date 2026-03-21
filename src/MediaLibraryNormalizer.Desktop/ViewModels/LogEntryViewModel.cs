using System;

namespace MediaLibraryNormalizer.Desktop.ViewModels;

public sealed class LogEntryViewModel
{
    public LogEntryViewModel(DateTime timestamp, string message)
    {
        Timestamp = timestamp;
        Message = message;
    }

    public DateTime Timestamp { get; }

    public string Message { get; }

    public string TimeText => Timestamp.ToString("HH:mm:ss");
}
