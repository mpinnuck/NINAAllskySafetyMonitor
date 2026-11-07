using NINAAllskySafetyMonitor.Core.Acquisition;
using NINAAllskySafetyMonitor.Core.Settings;

namespace NINAAllskySafetyMonitor.Tests.Fakes;

/// <summary>Connector to an in-memory "Pi"; can be told to throw on connect.</summary>
public sealed class FakeRemoteFileConnector : IRemoteFileConnector
{
    public DateTime ModifiedUtc { get; set; } = new(2026, 11, 1, 11, 0, 0, DateTimeKind.Utc);

    public byte[] Content { get; set; } = FakeImageSource.CompleteJpeg();

    public Exception? ConnectError { get; set; }

    public TimeSpan Hang { get; set; }

    public int Connects { get; private set; }

    public int Downloads { get; set; }

    public int Disposed { get; set; }

    public string? LastPath { get; set; }

    public async Task<IRemoteFileSession> ConnectAsync(DirectSourceSettings settings, TimeSpan connectTimeout, CancellationToken ct)
    {
        Connects++;
        if (Hang > TimeSpan.Zero) await Task.Delay(Hang, ct);
        if (ConnectError is not null) throw ConnectError;
        return new FakeRemoteFileSession(this);
    }
}
