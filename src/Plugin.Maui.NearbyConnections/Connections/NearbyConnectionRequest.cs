namespace Plugin.Maui.NearbyConnections;

sealed class NearbyConnectionRequest(
    NearbyDevice remoteDevice,
    Func<CancellationToken, Task<NearbyConnection>> accept,
    Func<CancellationToken, Task> reject)
{
    readonly Func<CancellationToken, Task<NearbyConnection>> _accept = accept;
    readonly Func<CancellationToken, Task> _reject = reject;

    public NearbyDevice RemoteDevice { get; } = remoteDevice;

    public Task<NearbyConnection> AcceptAsync(CancellationToken cancellationToken = default)
        => _accept(cancellationToken);

    public Task RejectAsync(CancellationToken cancellationToken = default)
        => _reject(cancellationToken);
}
