using NINAAllskySafetyMonitor.Core.Acquisition;
using NINAAllskySafetyMonitor.Core.Domain;

namespace NINAAllskySafetyMonitor.Tests.Fakes;

/// <summary>Image source returning queued results; repeats the last one when the queue runs out.</summary>
public sealed class FakeImageSource : IImageSource
{
    private readonly Queue<FetchResult> results = new();
    private FetchResult last = FetchResult.Failed("No result queued");

    public FakeImageSource(ImageSourceKind kind = ImageSourceKind.Direct) => Kind = kind;

    public ImageSourceKind Kind { get; }

    public int Fetches { get; private set; }

    public List<bool> ForceFlags { get; } = new();

    public int Resets { get; private set; }

    public FakeImageSource Then(FetchResult result)
    {
        results.Enqueue(result);
        return this;
    }

    public Task<FetchResult> FetchAsync(bool force, CancellationToken ct)
    {
        Fetches++;
        ForceFlags.Add(force);
        if (results.Count > 0) last = results.Dequeue();
        return Task.FromResult(last);
    }

    public void Reset() => Resets++;

    /// <summary>Smallest byte sequence the integrity check accepts as a whole JPEG.</summary>
    public static byte[] CompleteJpeg() => new byte[] { 0xFF, 0xD8, 0x01, 0x02, 0xFF, 0xD9 };

    /// <summary>A JPEG cut off part-way through.</summary>
    public static byte[] IncompleteJpeg() => new byte[] { 0xFF, 0xD8, 0x01, 0x02, 0x03, 0x04 };
}
