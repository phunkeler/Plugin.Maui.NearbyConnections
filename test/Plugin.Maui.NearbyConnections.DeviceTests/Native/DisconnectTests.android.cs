namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// Peer disconnection on Android (<c>OnDisconnected</c>): the connection's
/// <see cref="NearbyConnection.Disconnected"/> task completes and the platform's bookkeeping for
/// the peer is released.
/// </summary>
public class DisconnectTests : DeviceTest
{
    [Fact]
    public async Task RemoteDisconnect_CompletesDisconnectedTaskAndReleasesPeer()
    {
        // Arrange — a live connection established through the real callback path.
        await using var platform = Create.PlatformNearby();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var (connection, id) = await Create.ConnectedAsync(platform, "Alice", cts.Token);

        // Act
        platform.OnDisconnected(id);

        // Assert
        await connection.Disconnected.WaitAsync(cts.Token);
        Assert.False(platform._activeConnections.ContainsKey(id));
        Assert.False(platform.PeerLookup.TryGetDevice(id, out _));
    }
}
