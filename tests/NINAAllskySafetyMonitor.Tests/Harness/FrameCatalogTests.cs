using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Harness;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Harness;

[Requirement("REQ-42")]
public sealed class FrameCatalogTests : IDisposable
{
    private readonly ReplayFixture fx = new();

    public void Dispose() => fx.Dispose();

    private void Touch(string relative)
    {
        string path = Path.Combine(fx.Images, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[] { 1 });
    }

    [Fact]
    public void ListsNightFramesInTimeOrderIgnoringSubFoldersAndOtherFiles()
    {
        Touch("20261019/allsky-20261020022133.jpg");
        Touch("20261019/allsky-20261019220000.jpg");
        Touch("20261019/thumbnails/allsky-20261019220000.jpg");
        Touch("20261019/keogram-20261019.jpg");
        Touch("20261020/allsky-20261020230000.jpg");
        Touch("notanight/allsky-20261020230500.jpg");

        IReadOnlyList<SavedFrame> frames = new FrameCatalog().List(fx.Options(step: 1));

        Assert.Equal(new[] { "allsky-20261019220000.jpg", "allsky-20261020022133.jpg", "allsky-20261020230000.jpg" }, frames.Select(f => Path.GetFileName(f.Path)));
        Assert.Equal(new DateOnly(2026, 10, 19), frames[1].Night);
        Assert.Equal(SyntheticSky.FrameUtc, frames[1].CaptureUtc);
    }

    [Fact]
    public void NightsCanBeLimitedAndOneNightFolderGiven()
    {
        Touch("20261019/allsky-20261019220000.jpg");
        Touch("20261020/allsky-20261020230000.jpg");

        Assert.Single(new FrameCatalog().List(fx.Options() with { FirstNight = new DateOnly(2026, 10, 20) }));
        Assert.Single(new FrameCatalog().List(fx.Options() with { FramesFolder = Path.Combine(fx.Images, "20261019") }));
    }

    [Fact]
    public void FramesCloserThanTheStepAreSkipped()
    {
        foreach (string t in new[] { "220000", "220030", "220100", "220130", "220200" }) Touch($"20261019/allsky-20261019{t}.jpg");
        Assert.Equal(3, new FrameCatalog().List(fx.Options(step: 60)).Count);
        Assert.Equal(5, new FrameCatalog().List(fx.Options(step: 30)).Count);
    }

    [Fact]
    public void MissingFolderIsReported() =>
        Assert.Throws<DirectoryNotFoundException>(() => new FrameCatalog().List(fx.Options() with { FramesFolder = Path.Combine(fx.Root, "none") }));
}
