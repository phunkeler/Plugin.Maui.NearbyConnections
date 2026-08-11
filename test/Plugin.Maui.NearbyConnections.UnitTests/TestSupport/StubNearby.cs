using System.Diagnostics.CodeAnalysis;

namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// An inert <see cref="INearby"/> used only to prove that a consumer's own registration survives
/// <c>AddNearby</c>. Nothing calls its members; identity is the whole point.
/// </summary>
sealed class StubNearby : INearby
{
    public INearbyDevices Devices => throw new NotSupportedException();

    public bool IsAdvertising => false;

    public bool IsDiscovering => false;

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
