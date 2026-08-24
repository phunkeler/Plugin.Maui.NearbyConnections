namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// The iOS peer-identity stack against real <c>MCPeerID</c> instances: key derivation is stable
/// per peer and distinct across peers (even same-named ones), the local identity is memoized for
/// the process, and the registry round-trips native handles.
/// </summary>
public class PeerIdentityTests : DeviceTest
{
    [Fact]
    public void PeerKey_StablePerPeerAndDistinctAcrossPeers()
    {
        // Arrange
        var lookup = Create.PeerLookup();
        using var alice1 = new MCPeerID("Alice");
        using var alice2 = new MCPeerID("Alice");

        // Act
        var key1a = lookup.PeerKey(alice1);
        var key1b = lookup.PeerKey(alice1);
        var key2 = lookup.PeerKey(alice2);

        // Assert
        Assert.Equal(key1a, key1b);
        Assert.NotEqual(key1a, key2);
        Assert.Equal(16, key1a.Length); // hex of a truncated SHA-256 (8 bytes)
    }

    [Fact]
    public void PeerKey_NullPeer_ReturnsEmpty()
    {
        // Arrange
        var lookup = Create.PeerLookup();

        // Act
        var key = lookup.PeerKey(null!);

        // Assert
        Assert.Equal(string.Empty, key);
    }

    [Fact]
    public async Task LocalPeerIdentity_MemoizedForPlatformLifetime()
    {
        // Arrange
        await using var platform = Create.PlatformNearby(Create.DefaultOptions("Alice"));

        // Act
        var first = platform.GetLocalPeerId();
        var second = platform.GetLocalPeerId();

        // Assert
        Assert.Same(first, second);
        Assert.Equal("Alice", first.DisplayName);
    }

    [Fact]
    public async Task Registry_TracksAndReleasesNativeHandle()
    {
        // Arrange
        await using var platform = Create.PlatformNearby();
        using var peerId = Create.PeerId("Alice");

        // Act
        var device = platform.PeerLookup.Track(peerId);
        var hadHandle = platform.PeerLookup.TryGetHandle(device.Id, out var handle);
        platform.PeerLookup.Remove(device.Id);
        var hasHandleAfterRemove = platform.PeerLookup.TryGetHandle(device.Id, out _);

        // Assert
        Assert.True(hadHandle);
        Assert.Same(peerId, handle);
        Assert.False(hasHandleAfterRemove);
    }
}
