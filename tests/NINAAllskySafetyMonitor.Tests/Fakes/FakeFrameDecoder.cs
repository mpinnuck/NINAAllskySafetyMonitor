using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Domain;

namespace NINAAllskySafetyMonitor.Tests.Fakes;

/// <summary>Decoder returning the frame set by the test, whatever the JPEG bytes; null throws, like a corrupt file.</summary>
public sealed class FakeFrameDecoder : IFrameDecoder
{
    public GrayFrame? Frame { get; set; }

    public int Decodes { get; private set; }

    public GrayFrame Decode(byte[] jpeg)
    {
        Decodes++;
        return Frame ?? throw new InvalidDataException("not a JPEG");
    }
}
