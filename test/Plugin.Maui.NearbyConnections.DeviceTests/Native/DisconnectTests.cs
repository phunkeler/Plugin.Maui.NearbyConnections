namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// Peer disconnection (<c>OnDisconnected</c> on Android, <c>OnPeerStateChanged(NotConnected)</c> on
/// iOS): the connection's <see cref="NearbyConnection.Disconnected"/> task completes, the receive
/// stream ends, and the platform's bookkeeping for the peer is released.
/// </summary>
public class DisconnectTests
{
    [Fact]
    public async Task RemoteDisconnect_CompletesDisconnectedTaskAndReleasesPeer()
    {
        // Arrange — establish a live connection through the real callback path first.
        var platform = Create.PlatformNearby();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

#if ANDROID
        var (connection, id) = await Create.ConnectedAsync(platform, "Alice", cts.Token);

        // Act
        platform.OnDisconnected(id);
#elif IOS
        using var peerId = Create.PeerId("Alice");
        var (connection, id) = await Create.ConnectedAsync(platform, peerId, cts.Token);

        // Act
        platform.OnPeerStateChanged(peerId, MCSessionState.NotConnected);
#endif

        await connection.Disconnected.WaitAsync(cts.Token);

        // Assert
        Assert.False(platform._activeConnections.ContainsKey(id));
        Assert.False(platform.Peers.TryGetDevice(id, out _));
    }
}
