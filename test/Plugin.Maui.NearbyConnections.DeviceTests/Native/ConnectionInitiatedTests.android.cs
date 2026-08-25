namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// First leg of an inbound connection on Android: <c>OnConnectionInitiatedAsync</c> must surface a
/// <see cref="NearbyConnectionRequest"/> on the advertise channel. Exercised against the real
/// platform partial with real SDK callback types — no live radio.
/// </summary>
public class ConnectionInitiatedTests : DeviceTest
{
    [Fact]
    public async Task IncomingConnection_YieldsRequestOnAdvertiseChannel()
    {
        // Arrange
        await using var platform = Create.PlatformNearby();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        await platform.AndroidAdapter.OnConnectionInitiatedAsync("endpoint-1", Create.ConnectionInfo());

        // Assert
        var request = await platform._advertiseChannel.Reader.ReadAsync(cts.Token);
        Assert.Equal("Alice", request.RemoteDevice.DisplayName);
    }
}
