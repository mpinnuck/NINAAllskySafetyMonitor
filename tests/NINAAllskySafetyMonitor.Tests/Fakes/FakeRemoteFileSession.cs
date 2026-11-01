using NINAAllskySafetyMonitor.Core.Acquisition;

namespace NINAAllskySafetyMonitor.Tests.Fakes;

/// <summary>Remote session serving one file from memory.</summary>
public sealed class FakeRemoteFileSession : IRemoteFileSession
{
    private readonly FakeRemoteFileConnector owner;

    public FakeRemoteFileSession(FakeRemoteFileConnector owner) => this.owner = owner;

    public DateTime GetLastWriteTimeUtc(string path)
    {
        owner.LastPath = path;
        return owner.ModifiedUtc;
    }

    public byte[] ReadAllBytes(string path)
    {
        owner.Downloads++;
        return owner.Content;
    }

    public void Dispose() => owner.Disposed++;
}
