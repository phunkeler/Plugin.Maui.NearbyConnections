namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// The iOS peer-identity stack against real <c>MCPeerID</c> instances: key derivation is stable
/// per peer and distinct across peers (even same-named ones), the local identity is memoized for
/// the process, and the registry round-trips native handles.
/// </summary>
public class PeerIdentityTests
{
    [Fact]
    public void PeerKey_StablePerPeerAndDistinctAcrossPeers()
    {
        // Arrange — two DIFFERENT MCPeerID instances with the SAME display name: MPC identity is
        // per-instance, and the key must follow the identity, not the name.
        var registry = Create.PeerRegistry();
        using var alice1 = new MCPeerID("Alice");
        using var alice2 = new MCPeerID("Alice");

        // Act
        var key1a = registry.PeerKey(alice1);
        var key1b = registry.PeerKey(alice1);
        var key2 = registry.PeerKey(alice2);

        // Assert
        Assert.Equal(key1a, key1b);
        Assert.NotEqual(key1a, key2);
        Assert.Equal(16, key1a.Length); // hex of a truncated SHA-256 (8 bytes)
    }

    [Fact]
    public void PeerKey_NullPeer_ReturnsEmpty()
    {
        // Arrange
        var registry = Create.PeerRegistry();

        // Act
        var key = registry.PeerKey(null!);

        // Assert
        Assert.Equal(string.Empty, key);
    }

    [Fact]
    public void LocalPeerIdentity_MemoizedForProcessLifetime()
    {
        // Arrange
        var registry = Create.PeerRegistry("Alice");

        // Act
        var first = registry.GetLocalPeerId();
        var second = registry.GetLocalPeerId();

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
        var device = platform.Peers.Track(peerId);
        var hadHandle = platform.Peers.TryGetHandle(device.Id, out var handle);
        platform.Peers.Remove(device.Id);
        var hasHandleAfterRemove = platform.Peers.TryGetHandle(device.Id, out _);

        // Assert
        Assert.True(hadHandle);
        Assert.Same(peerId, handle);
        Assert.False(hasHandleAfterRemove);
    }
}
