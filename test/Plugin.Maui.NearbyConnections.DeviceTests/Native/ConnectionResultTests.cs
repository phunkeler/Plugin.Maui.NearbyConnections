namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// The terminal "connection succeeded/failed" callback (<c>OnConnectionResult</c> on Android,
/// <c>OnPeerStateChanged</c> on iOS). The repo's documented invariant: every failure path must
/// resolve or fault the pending <c>_connectionTcs</c> entry, or <c>AcceptAsync</c>/<c>ConnectAsync</c>
/// hang forever.
/// </summary>
public class ConnectionResultTests
{
    [Fact]
    public async Task Success_ResolvesConnectionTcsAndRegistersActiveConnection()
    {
        // Arrange
        var platform = Create.PlatformNearby();
        var tcs = new TaskCompletionSource<NearbyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);

#if ANDROID
        const string id = "endpoint-1";
        platform.Peers.Record(id, "Alice");
        platform._connectionTcs[id] = (tcs, CancellationToken.None);

        // ConnectionResolution's ctor is deprecated-but-shipped at the pinned binding version;
        // constructing results directly is the same pattern dotnet/maui's Essentials device tests use.
        var resolution = Create.Resolution();

        // Act
        platform.OnConnectionResult(id, resolution);
#elif IOS
        using var peerId = Create.PeerId("Alice");
        var id = platform.Peers.PeerKeyProvider.PeerKey(peerId);
        platform._connectionTcs[id] = (tcs, CancellationToken.None);

        // Act — Connected with NO prior Connecting: iOS does not guarantee the Connecting waypoint.
        platform.OnPeerStateChanged(peerId, MCSessionState.Connected);
#endif

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var connection = await tcs.Task.WaitAsync(cts.Token);

        // Assert
        Assert.Equal("Alice", connection.RemoteDevice.DisplayName);
        Assert.True(platform._activeConnections.ContainsKey(id));
    }

    [Fact]
    public async Task Failure_FaultsConnectionTcs()
    {
        // Arrange
        var platform = Create.PlatformNearby();
        var tcs = new TaskCompletionSource<NearbyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);

#if ANDROID
        const string id = "endpoint-1";
        platform.Peers.Record(id, "Alice");
        platform._connectionTcs[id] = (tcs, CancellationToken.None);

        var resolution = Create.Resolution(ConnectionsStatusCodes.StatusConnectionRejected);

        // Act
        platform.OnConnectionResult(id, resolution);
#elif IOS
        using var peerId = Create.PeerId("Alice");
        var id = platform.Peers.PeerKeyProvider.PeerKey(peerId);
        platform._connectionTcs[id] = (tcs, CancellationToken.None);

        // Act — straight to NotConnected with no prior Connecting: the documented latent-hang path.
        platform.OnPeerStateChanged(peerId, MCSessionState.NotConnected);
#endif

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Assert
        await Assert.ThrowsAsync<NearbyException>(() => tcs.Task.WaitAsync(cts.Token));
        Assert.False(platform._activeConnections.ContainsKey(id));
    }
}
