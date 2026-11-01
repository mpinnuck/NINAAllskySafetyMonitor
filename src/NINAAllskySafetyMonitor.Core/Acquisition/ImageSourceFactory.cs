using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Acquisition;

/// <summary>Holds one source of each kind and returns the one selected in the settings.</summary>
[Requirement("REQ-11")]
public sealed class ImageSourceFactory
{
    private readonly IImageSource web;
    private readonly IImageSource direct;

    public ImageSourceFactory(IImageSource web, IImageSource direct)
    {
        this.web = web ?? throw new ArgumentNullException(nameof(web));
        this.direct = direct ?? throw new ArgumentNullException(nameof(direct));
    }

    /// <summary>The source for <paramref name="kind"/>. Replay is not a live source.</summary>
    public IImageSource For(ImageSourceKind kind) => kind switch
    {
        ImageSourceKind.Web => web,
        ImageSourceKind.Direct => direct,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a live image source"),
    };
}
