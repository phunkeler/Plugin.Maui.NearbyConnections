namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// Discovery callbacks on iOS (<c>FoundPeer</c>/<c>LostPeer</c>): found and lost events arrive on
/// the discover channel, and the peer registry tracks and releases the device.
/// </summary>
public class DeviceDiscoveryTests
{
    [Fact]
    public async Task FoundPeer_PublishesFoundEventAndTracksPeer()
    {
        // Arrange
        await using var platform = Create.PlatformNearby();
        using var peerId = Create.PeerId("Alice");
        var id = platform.Peers.PeerKeyProvider.PeerKey(peerId);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        platform.FoundPeer(browser: null!, peerID: peerId, info: null);

        // Assert
        var found = await platform._discoverChannel.Reader.ReadAsync(cts.Token);
        Assert.Equal(NearbyDeviceEventType.Found, found.Type);
        Assert.True(platform.Peers.TryGetDevice(id, out var tracked));
        Assert.Equal("Alice", tracked.DisplayName);
    }

    [Fact]
    public async Task LostPeer_PublishesLostEventForTheSameDeviceAndReleasesPeer()
    {
        // Arrange — a peer already discovered, and its Found event drained off the channel.
        await using var platform = Create.PlatformNearby();
        using var peerId = Create.PeerId("Alice");
        var id = platform.Peers.PeerKeyProvider.PeerKey(peerId);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        platform.FoundPeer(browser: null!, peerID: peerId, info: null);
        var found = await platform._discoverChannel.Reader.ReadAsync(cts.Token);

        // Act
        platform.LostPeer(browser: null!, peerID: peerId);

        // Assert
        var lost = await platform._discoverChannel.Reader.ReadAsync(cts.Token);
        Assert.Equal(NearbyDeviceEventType.Lost, lost.Type);
        Assert.Equal(found.Device.Id, lost.Device.Id);
        Assert.False(platform.Peers.TryGetDevice(id, out _));
    }

    [Fact]
    public async Task LostPeer_WhileConnected_DoesNotEmitLostEvent()
    {
        // Arrange — a connected peer that stops advertising is NOT lost; only its advertisement is.
        await using var platform = Create.PlatformNearby();
        using var peerId = Create.PeerId("Alice");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var (_, id) = await Create.ConnectedAsync(platform, peerId, cts.Token);

        // Act
        platform.LostPeer(browser: null!, peerID: peerId);

        // Assert
        Assert.True(platform.Peers.TryGetDevice(id, out _));
        Assert.False(platform._discoverChannel.Reader.TryRead(out _));
    }
}
