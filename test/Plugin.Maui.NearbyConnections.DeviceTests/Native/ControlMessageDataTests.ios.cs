namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// Control frames arriving through <c>OnDataReceived</c> are consumed by the plugin, never
/// surfaced to the consumer as payloads. (Named to avoid colliding with the unit suite's
/// <c>ControlMessageTests</c>, which covers Encode/TryDecode on <c>net10.0</c>.)
/// </summary>
public class ControlMessageDataTests
{
    static CancellationTokenSource Timeout() => new(TimeSpan.FromSeconds(5));

    [Fact]
    public async Task DisconnectControlFrame_IsNotSurfacedAsPayload()
    {
        // Arrange
        await using var platform = Create.PlatformNearby();
        using var alice = Create.PeerId("Alice");
        using var timeout = Timeout();
        var (connection, _) = await Create.ConnectedAsync(platform, alice, timeout.Token);

        // Act
        Deliver.ControlFrame(platform, alice, ControlMessageType.Disconnect);

        // Assert
        await Receive.AssertNothingReceivedAsync(connection);
    }

    [Fact]
    public async Task UnknownControlType_IsSwallowedWithoutThrowing()
    {
        // Arrange
        await using var platform = Create.PlatformNearby();
        using var alice = Create.PeerId("Alice");
        using var timeout = Timeout();
        var (connection, _) = await Create.ConnectedAsync(platform, alice, timeout.Token);

        // Act
        Deliver.UnknownControlFrame(platform, alice);

        // Assert
        await Receive.AssertNothingReceivedAsync(connection);
    }

    [Fact]
    public async Task DisconnectControlFrame_ReleasesTheSendingPeer()
    {
        // Arrange
        await using var platform = Create.PlatformNearby();
        using var alice = Create.PeerId("Alice");
        using var timeout = Timeout();
        var (_, aliceId) = await Create.ConnectedAsync(platform, alice, timeout.Token);

        // Act
        Deliver.ControlFrame(platform, alice, ControlMessageType.Disconnect);

        // Assert
        Assert.False(platform._activeConnections.ContainsKey(aliceId));
        Assert.False(platform.Peers.TryGetDevice(aliceId, out _));
    }

    [Fact]
    public async Task DisconnectControlFrame_LeavesOtherPeersConnected()
    {
        // Arrange — MCSession.Disconnect() is all-or-nothing, so a departing peer must not take
        // Bob's connection with it. This is the multi-peer case single-peer tests cannot see.
        await using var platform = Create.PlatformNearby();
        using var alice = Create.PeerId("Alice");
        using var bob = Create.PeerId("Bob");
        using var timeout = Timeout();
        await Create.ConnectedAsync(platform, alice, timeout.Token);
        var (_, bobId) = await Create.ConnectedAsync(platform, bob, timeout.Token);

        // Act
        Deliver.ControlFrame(platform, alice, ControlMessageType.Disconnect);

        // Assert
        Assert.True(platform._activeConnections.ContainsKey(bobId));
        Assert.True(platform.Peers.TryGetDevice(bobId, out _));
    }

    [Fact]
    public async Task AfterAPeerDeparts_ARemainingPeerStillReceivesPayloads()
    {
        // Arrange
        await using var platform = Create.PlatformNearby();
        using var alice = Create.PeerId("Alice");
        using var bob = Create.PeerId("Bob");
        using var timeout = Timeout();
        await Create.ConnectedAsync(platform, alice, timeout.Token);
        var (bobConnection, _) = await Create.ConnectedAsync(platform, bob, timeout.Token);
        byte[] expected = [0x07];
        Deliver.ControlFrame(platform, alice, ControlMessageType.Disconnect);

        // Act
        Deliver.Bytes(platform, bob, expected);

        // Assert
        var received = await Receive.FirstAsync(bobConnection, timeout.Token);
        Assert.Equal(expected, Assert.IsType<NearbyBytesPayload>(received).Data);
    }

    [Fact]
    public async Task DisconnectControlFrame_FromTheLastPeer_EmptiesTheRegistry()
    {
        // Arrange — one peer, so its departure is the last one and the session may be torn down.
        await using var platform = Create.PlatformNearby();
        using var alice = Create.PeerId("Alice");
        using var timeout = Timeout();
        await Create.ConnectedAsync(platform, alice, timeout.Token);

        // Act
        Deliver.ControlFrame(platform, alice, ControlMessageType.Disconnect);

        // Assert
        Assert.True(platform.Peers.IsEmpty);
    }
}
