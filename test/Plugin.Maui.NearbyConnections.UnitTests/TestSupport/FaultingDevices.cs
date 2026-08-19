using System.Diagnostics.CodeAnalysis;

namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// An <see cref="INearby"/> whose <see cref="INearbyDevices.Changes"/> stream faults on its first
/// move, so a test can observe how <see cref="NearbyDeviceCollection{TRow}"/> reports a broken
/// stream.
/// </summary>
/// <remarks>
/// <see cref="FakeNearby"/> stands in for the platform seam and cannot fault the registry's stream
/// from above it. This fake replaces the session outright, which is the only way to reach the
/// collection's stream-fault path.
/// </remarks>
sealed class FaultingDevices : INearby, INearbyDevices
{
    /// <summary>The exception the change stream throws. Identity is what the test asserts on.</summary>
    public Exception Fault { get; } = new InvalidOperationException("stream broke");

    public INearbyDevices Devices => this;

    public IAsyncEnumerable<NearbyDeviceChange> Changes => FaultingStream();

    public int Count => 0;

    public NearbyDevice this[int index] => throw new ArgumentOutOfRangeException(nameof(index));

    public IEnumerator<NearbyDevice> GetEnumerator()
    {
        yield break;
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    async IAsyncEnumerable<NearbyDeviceChange> FaultingStream()
    {
        await Task.Yield();
        throw Fault;

#pragma warning disable CS0162 // Unreachable: an iterator needs a yield to be an iterator at all.
        yield break;
#pragma warning restore CS0162
    }

    public bool IsAdvertising => false;

    public bool IsDiscovering => false;

    public IAsyncEnumerable<bool> AdvertisingChanges => throw new NotSupportedException();

    public IAsyncEnumerable<bool> DiscoveryChanges => throw new NotSupportedException();

    public Task<NearbyAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task StartAdvertisingAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task StopAdvertisingAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task StartDiscoveryAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task StopDiscoveryAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task StopAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<NearbyConnection> ConnectAsync(NearbyDevice device, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<NearbyConnection> AcceptAsync(NearbyDevice device, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task RejectAsync(NearbyDevice device, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task DisconnectAsync(NearbyDevice device, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public bool TryGetConnection(string deviceId, out NearbyConnection connection)
        => throw new NotSupportedException();

    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Implements INearby.DisposeAsync; an interface implementation cannot be static.")]
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
