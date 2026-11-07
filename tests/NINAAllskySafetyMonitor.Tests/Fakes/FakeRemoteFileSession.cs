using NINAAllskySafetyMonitor.Core.Acquisition;

namespace NINAAllskySafetyMonitor.Tests.Fakes;

/// <summary>Remote session serving one file from memory.</summary>
public sealed class FakeRemoteFileSession : IRemoteFileSession
{
    private readonly FakeRemoteFileConnector owner;

    public FakeRemoteFileSession(FakeRemoteFileConnector owner) => this.owner = owner;

    public Task<DateTime> GetLastWriteTimeUtcAsync(string path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        owner.LastPath = path;
        return Task.FromResult(owner.ModifiedUtc);
    }

    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        owner.Downloads++;
        return Task.FromResult(owner.Content);
    }

    public void Dispose() => owner.Disposed++;
}
