namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// MPC session-state transitions beyond the shared success/failure cases:
/// <c>MCSessionState.Connecting</c> is optional and never a required waypoint — the documented
/// latent-hang class from AGENTS.md.
/// </summary>
public class SessionStateTests : DeviceTest
{
    [Fact]
    public async Task Connecting_LeavesHandshakePending()
    {
        // Arrange
        await using var platform = Create.PlatformNearby();
        using var peerID = Create.PeerId("Alice");
        var id = platform.PeerLookup.DeviceIdFor(peerID);
        var tcs = new TaskCompletionSource<NearbyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
        platform._connectionTcs[id] = (tcs, CancellationToken.None);

        // Act
        platform.OnPeerStateChanged(peerID, MCSessionState.Connecting);

        // Assert — Connecting is informational; the handshake is neither resolved nor faulted.
        Assert.False(tcs.Task.IsCompleted);
    }

    [Fact]
    public async Task ConnectingThenNotConnected_FaultsHandshake()
    {
        // Arrange — the invitation-declined shape: Connecting arrives, then NotConnected.
        await using var platform = Create.PlatformNearby();
        using var peerID = Create.PeerId("Alice");
        var id = platform.PeerLookup.DeviceIdFor(peerID);
        var tcs = new TaskCompletionSource<NearbyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
        platform._connectionTcs[id] = (tcs, CancellationToken.None);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        platform.OnPeerStateChanged(peerID, MCSessionState.Connecting);
        platform.OnPeerStateChanged(peerID, MCSessionState.NotConnected);

        // Assert
        await Assert.ThrowsAsync<NearbyException>(() => tcs.Task.WaitAsync(cts.Token));
    }
}
