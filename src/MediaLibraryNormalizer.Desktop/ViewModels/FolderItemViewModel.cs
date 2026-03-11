using MediaLibraryNormalizer.Models;

namespace MediaLibraryNormalizer.Desktop.ViewModels;

public class FolderItemViewModel
{
    public FolderItemViewModel(MediaItem item)
    {
        Name = item.OriginalName;
        Path = item.Path;
        FileCount = item.FileCount;
    }

    public string Name { get; }

    public string Path { get; }

    public int FileCount { get; }

    public string FileCountText => FileCount == 1 ? "1 video file" : $"{FileCount} video files";
}
