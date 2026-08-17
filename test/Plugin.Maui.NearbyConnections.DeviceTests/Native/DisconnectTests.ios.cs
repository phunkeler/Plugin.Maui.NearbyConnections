namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// Peer disconnection on iOS (<c>OnPeerStateChanged(NotConnected)</c> against a live connection):
/// the connection's <see cref="NearbyConnection.Disconnected"/> task completes and the platform's
/// bookkeeping for the peer is released.
/// </summary>
public class DisconnectTests
{
    [Fact]
    public async Task RemoteDisconnect_CompletesDisconnectedTaskAndReleasesPeer()
    {
        // Arrange — a live connection established through the real callback path.
        await using var platform = Create.PlatformNearby();
        using var peerId = Create.PeerId("Alice");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var (connection, id) = await Create.ConnectedAsync(platform, peerId, cts.Token);

        // Act
        platform.OnPeerStateChanged(peerId, MCSessionState.NotConnected);

        // Assert
        await connection.Disconnected.WaitAsync(cts.Token);
        Assert.False(platform._activeConnections.ContainsKey(id));
        Assert.False(platform.Peers.TryGetDevice(id, out _));
    }

    [Fact]
    public async Task LocalDisconnect_ReleasesTheConnectionButKeepsThePeerReconnectable()
    {
        // Arrange
        await using var platform = Create.PlatformNearby();
        using var peerId = Create.PeerId("Alice");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var (connection, id) = await Create.ConnectedAsync(platform, peerId, cts.Token);

        // Act
        await connection.DisposeAsync();

        // Assert
        Assert.False(platform._activeConnections.ContainsKey(id));
        Assert.True(platform.Peers.TryGetDevice(id, out _));
        Assert.True(platform.Peers.TryGetHandle(id, out _));
    }
}
