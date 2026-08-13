namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// Discovery callbacks (<c>OnEndpointFound</c>/<c>OnEndpointLost</c> on Android,
/// <c>FoundPeer</c>/<c>LostPeer</c> on iOS): found/lost events arrive on the discover channel in
/// order, and the peer registry tracks and releases the device.
/// </summary>
public class DeviceDiscoveryTests
{
    [Fact]
    public async Task FoundThenLost_YieldsOrderedEventsAndReleasesPeer()
    {
        // Arrange
        var platform = Create.PlatformNearby();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

#if ANDROID
        const string id = "endpoint-1";
        var info = Create.DiscoveredEndpointInfo();

        // Act
        platform.OnEndpointFound(id, info);
        var foundDevice = platform.Peers.TryGetDevice(id, out var tracked);
        platform.OnEndpointLost(id);
#elif IOS
        using var peerId = Create.PeerId("Alice");
        var id = platform.Peers.PeerKeyProvider.PeerKey(peerId);

        // Act
        platform.FoundPeer(browser: null!, peerID: peerId, info: null);
        var foundDevice = platform.Peers.TryGetDevice(id, out var tracked);
        platform.LostPeer(browser: null!, peerID: peerId);
#endif

        var reader = platform._discoverChannel.Reader;
        var first = await reader.ReadAsync(cts.Token);
        var second = await reader.ReadAsync(cts.Token);

        // Assert
        Assert.True(foundDevice);
        Assert.Equal("Alice", tracked!.DisplayName);
        Assert.Equal(NearbyDeviceEventType.Found, first.Type);
        Assert.Equal(NearbyDeviceEventType.Lost, second.Type);
        Assert.Equal(first.Device.Id, second.Device.Id);
        Assert.False(platform.Peers.TryGetDevice(id, out _));
    }

    [Fact]
    public async Task LostWhileConnected_DoesNotEmitLostEvent()
    {
        // Arrange — a connected peer that stops advertising is NOT lost; only its advertisement is.
        var platform = Create.PlatformNearby();
        var tcs = new TaskCompletionSource<NearbyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

#if ANDROID
        const string id = "endpoint-1";
        platform.Peers.Record(id, "Alice");
        platform._connectionTcs[id] = (tcs, CancellationToken.None);
        platform.OnConnectionResult(id, Create.Resolution());
        await tcs.Task.WaitAsync(cts.Token);

        // Act
        platform.OnEndpointLost(id);
#elif IOS
        using var peerId = Create.PeerId("Alice");
        var id = platform.Peers.PeerKeyProvider.PeerKey(peerId);
        platform._connectionTcs[id] = (tcs, CancellationToken.None);
        platform.OnPeerStateChanged(peerId, MCSessionState.Connected);
        await tcs.Task.WaitAsync(cts.Token);

        // Act
        platform.LostPeer(browser: null!, peerID: peerId);
#endif

        // Assert — the peer survives in the registry and no Lost event was published.
        Assert.True(platform.Peers.TryGetDevice(id, out _));
        Assert.False(platform._discoverChannel.Reader.TryRead(out _));
    }
}
