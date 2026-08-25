namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// The availability preflight against real Google Play Services and real radio/permission state.
/// The result is environment-dependent (emulator image, granted permissions), so the contract
/// pinned here is the documented one: the check completes, never throws, and never prompts.
/// </summary>
public class AvailabilityTests : DeviceTest
{
    [Fact]
    public async Task CheckAvailability_CompletesWithoutThrowing()
    {
        // Arrange
        await using var platform = Create.PlatformBridge();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        var result = await platform.CheckAvailabilityAsync(cts.Token);

        // Assert — every reported flag is a defined NearbyAvailability value.
        var allKnown = NearbyAvailability.Ready
            | NearbyAvailability.MissingPermissions
            | NearbyAvailability.BluetoothDisabled
            | NearbyAvailability.WifiDisabled
            | NearbyAvailability.PlayServicesUnavailable
            | NearbyAvailability.UnsupportedPlatform
            | NearbyAvailability.InvalidConfiguration;
        Assert.Equal(result, result & allKnown);
    }
}
