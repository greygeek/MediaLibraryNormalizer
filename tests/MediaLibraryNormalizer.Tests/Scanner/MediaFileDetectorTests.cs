using MediaLibraryNormalizer.Scanner;

namespace MediaLibraryNormalizer.Tests.Scanner;

public class MediaFileDetectorTests
{
    private readonly MediaFileDetector _sut = new();

    // === Video file detection ===

    [Theory]
    [InlineData("show.mkv", true)]
    [InlineData("show.mp4", true)]
    [InlineData("show.avi", true)]
    [InlineData("show.m4v", true)]
    [InlineData("show.mov", true)]
    [InlineData("show.ts", true)]
    public void IsVideoFile_ValidExtensions_ReturnsTrue(string fileName, bool expected)
    {
        Assert.Equal(expected, _sut.IsVideoFile(fileName));
    }

    [Theory]
    [InlineData("show.txt")]
    [InlineData("show.jpg")]
    [InlineData("show.nfo")]
    [InlineData("show.srt")]
    [InlineData("show.exe")]
    public void IsVideoFile_NonVideoExtensions_ReturnsFalse(string fileName)
    {
        Assert.False(_sut.IsVideoFile(fileName));
    }

    [Theory]
    [InlineData("show.MKV")]
    [InlineData("show.Mp4")]
    [InlineData("show.AVI")]
    public void IsVideoFile_CaseInsensitive(string fileName)
    {
        Assert.True(_sut.IsVideoFile(fileName));
    }

    // === Sample file detection ===

    [Theory]
    [InlineData("show.sample.mkv", true)]            // .sample. between dots
    [InlineData("show.sample.720p.mkv", true)]       // .sample. in the middle
    [InlineData("sample.show.mkv", true)]            // sample. at the start
    public void IsSampleFile_SamplePatterns_DetectedCorrectly(string fileName, bool expected)
    {
        Assert.Equal(expected, _sut.IsSampleFile(fileName));
    }

    [Theory]
    [InlineData("show.mkv")]
    [InlineData("The.Sampler.S01E01.mkv")]
    [InlineData("grand.designs.s18e03.720p.hdtv.x264-qpel-sample.mkv")] // release group named "qpel-sample"
    [InlineData("show.S01E01.HDTV.x264-GROUP-sample.mkv")]               // group suffix after dash — not a sample
    public void IsSampleFile_NormalFiles_ReturnsFalse(string fileName)
    {
        Assert.False(_sut.IsSampleFile(fileName));
    }

    [Fact]
    public void IsVideoFile_SampleVideo_ReturnsFalse()
    {
        // Sample videos should be excluded from video file detection
        Assert.False(_sut.IsVideoFile("show.sample.mkv"));
    }

    // === Associated file detection ===

    [Theory]
    [InlineData("show.srt", true)]
    [InlineData("show.ssa", true)]
    [InlineData("show.ass", true)]
    [InlineData("show.sub", true)]
    [InlineData("show.idx", true)]
    [InlineData("show.nfo", true)]
    [InlineData("show.txt", true)]
    [InlineData("show.jpg", true)]
    [InlineData("show.png", true)]
    public void IsAssociatedFile_ValidExtensions_ReturnsTrue(string fileName, bool expected)
    {
        Assert.Equal(expected, _sut.IsAssociatedFile(fileName));
    }

    [Theory]
    [InlineData("show.mkv")]
    [InlineData("show.exe")]
    public void IsAssociatedFile_NonAssociatedFiles_ReturnsFalse(string fileName)
    {
        Assert.False(_sut.IsAssociatedFile(fileName));
    }

    [Theory]
    [InlineData("show.srt")]
    [InlineData("show.ass")]
    [InlineData("show.idx")]
    public void IsSubtitleFile_SubtitleExtensions_ReturnsTrue(string fileName)
    {
        Assert.True(_sut.IsSubtitleFile(fileName));
    }

    [Theory]
    [InlineData("show.nfo")]
    [InlineData("show.jpg")]
    [InlineData("show.txt")]
    public void IsSubtitleFile_NonSubtitleExtensions_ReturnsFalse(string fileName)
    {
        Assert.False(_sut.IsSubtitleFile(fileName));
    }

    [Theory]
    [InlineData("show.nfo")]
    [InlineData("show.txt")]
    [InlineData("show.sfv")]
    [InlineData("show.jpg")]
    [InlineData("show.jpeg")]
    [InlineData("show.png")]
    public void IsMovieArtifactFile_MetadataExtensions_ReturnTrue(string fileName)
    {
        Assert.True(_sut.IsMovieArtifactFile(fileName));
    }

    [Theory]
    [InlineData("show.srt")]
    [InlineData("show.mkv")]
    public void IsMovieArtifactFile_ProtectedExtensions_ReturnFalse(string fileName)
    {
        Assert.False(_sut.IsMovieArtifactFile(fileName));
    }
}
