namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// Every Android platform callback tolerates an unknown endpoint id without throwing — the repo's
/// "every catch on a callback path logs" rule means a stray late callback must never take down the
/// process. Each Act would fail the test on throw; the assert pins the absence of side effects.
/// </summary>
public class UnknownPeerTests : DeviceTest
{
    const string UnknownId = "never-seen";

    [Fact]
    public async Task Disconnected_ForUnknownEndpoint_LeavesNoState()
    {
        // Arrange
        await using var platform = Create.PlatformNearby();

        // Act
        platform.AndroidAdapter.OnDisconnected(UnknownId);

        // Assert
        Assert.False(platform._activeConnections.ContainsKey(UnknownId));
        Assert.False(platform._discoverChannel.Reader.TryRead(out _));
    }

    [Fact]
    public async Task EndpointLost_ForUnknownEndpoint_LeavesNoState()
    {
        // Arrange
        await using var platform = Create.PlatformNearby();

        // Act
        platform.AndroidAdapter.OnEndpointLost(UnknownId);

        // Assert
        Assert.False(platform._activeConnections.ContainsKey(UnknownId));
        Assert.False(platform._discoverChannel.Reader.TryRead(out _));
    }

    [Fact]
    public async Task ConnectionResult_ForUnknownEndpoint_RegistersNoConnection()
    {
        // Arrange
        await using var platform = Create.PlatformNearby();

        // Act
        platform.AndroidAdapter.OnConnectionResult(UnknownId, Create.Resolution());

        // Assert
        Assert.False(platform._activeConnections.ContainsKey(UnknownId));
        Assert.False(platform._advertiseChannel.Reader.TryRead(out _));
    }

    [Fact]
    public async Task PayloadTransferUpdate_ForUnknownEndpoint_RegistersNoConnection()
    {
        // Arrange
        await using var platform = Create.PlatformNearby();

        // Act
        await platform.AndroidAdapter.OnPayloadTransferUpdate(
            UnknownId, Create.TransferUpdate(payloadId: 42, PayloadTransferUpdate.Status.Success));

        // Assert
        Assert.False(platform._activeConnections.ContainsKey(UnknownId));
        Assert.False(platform._advertiseChannel.Reader.TryRead(out _));
    }
}
