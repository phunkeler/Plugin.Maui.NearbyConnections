namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// Discovery callbacks on Android (<c>OnEndpointFound</c>/<c>OnEndpointLost</c>): found and lost
/// events arrive on the discover channel, and the peer registry tracks and releases the device.
/// </summary>
public class DeviceDiscoveryTests : DeviceTest
{
    [Fact]
    public async Task EndpointFound_PublishesFoundEventAndTracksPeer()
    {
        // Arrange
        await using var platform = Create.PlatformNearby();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        platform.OnEndpointFound("endpoint-1", Create.DiscoveredEndpointInfo());

        // Assert
        var found = await platform._discoverChannel.Reader.ReadAsync(cts.Token);
        Assert.True(found.Found);
        Assert.True(platform.PeerLookup.TryGetDevice("endpoint-1", out var tracked));
        Assert.Equal("Alice", tracked.DisplayName);
    }

    [Fact]
    public async Task EndpointLost_PublishesLostEventForTheSameDeviceAndReleasesPeer()
    {
        // Arrange — a device already discovered, and its Found event drained off the channel.
        await using var platform = Create.PlatformNearby();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        platform.OnEndpointFound("endpoint-1", Create.DiscoveredEndpointInfo());
        var found = await platform._discoverChannel.Reader.ReadAsync(cts.Token);

        // Act
        platform.OnEndpointLost("endpoint-1");

        // Assert
        var lost = await platform._discoverChannel.Reader.ReadAsync(cts.Token);
        Assert.False(lost.Found);
        Assert.Equal(found.Device.Id, lost.Device.Id);
        Assert.False(platform.PeerLookup.TryGetDevice("endpoint-1", out _));
    }

    [Fact]
    public async Task EndpointLost_WhileConnected_DoesNotEmitLostEvent()
    {
        // Arrange — a connected peer that stops advertising is NOT lost; only its advertisement is.
        await using var platform = Create.PlatformNearby();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Create.ConnectedAsync(platform, "Alice", cts.Token);

        // Act
        platform.OnEndpointLost("endpoint-1");

        // Assert
        Assert.True(platform.PeerLookup.TryGetDevice("endpoint-1", out _));
        Assert.False(platform._discoverChannel.Reader.TryRead(out _));
    }
}
