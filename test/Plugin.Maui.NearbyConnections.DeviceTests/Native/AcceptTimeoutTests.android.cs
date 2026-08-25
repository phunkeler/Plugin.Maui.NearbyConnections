namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// The accept path's own deadline on Android. <c>AcceptAsync</c> awaits a
/// <see cref="TaskCompletionSource{TResult}"/> that only a terminal GMS callback resolves, and a
/// device that leaves range mid-handshake produces no such callback. Without
/// <see cref="NearbyOptions.AcceptTimeout"/> the await never returns.
/// </summary>
/// <remarks>
/// Real time, not a fake clock: these run against the real platform partial, so the timeouts are
/// deliberately short rather than injected.
/// <para>
/// <strong>Requires a real peer, so skipped in the unattended CI/local run.</strong>
/// <c>AcceptAsync</c> calls the real GMS <c>AcceptConnectionAsync</c>, which validates against
/// GMS's own live per-endpoint connection state. That state only exists once GMS has itself
/// processed a genuine <c>requestConnection</c> from a second device — a synthetic
/// <c>OnConnectionInitiatedAsync</c> call does not create it, so on a single, radio-isolated
/// emulator GMS always rejects the accept with <c>STATUS_OUT_OF_ORDER_API_CALL</c> (8009),
/// verified 2026-08-23 against a real advertising session (<see cref="IPlatformNearby.AdvertiseAsync"/>)
/// and not just a channel write. Same "call the real GMS-backed API, tag what needs a live
/// environment" pattern as dotnet/maui's own device tests
/// (<c>Geolocation_Tests.cs</c>, <c>Traits.InteractionType</c>/<c>Human</c>), which is why these
/// are excluded by trait rather than deleted or faked. The timeout mechanics themselves
/// (<c>AwaitHandshakeAsync</c>) still have unit coverage that runs everywhere.
/// </para>
/// </remarks>
public class AcceptTimeoutTests : DeviceTest
{
    /// <summary>
    /// Marks a test that needs a real second device or radio and so cannot run against a single,
    /// radio-isolated emulator/simulator. Excluded from the unattended device-test run via
    /// <c>dotnet test --filter</c> in <c>scripts/device-tests.ps1</c>.
    /// </summary>
    const string RequiresRealPeerTrait = "RequiresRealPeer";

    static NearbyOptions Options(TimeSpan accept) => new()
    {
        ServiceId = "devicetests",
        AcceptTimeout = accept,
    };

    [Fact]
    [Trait("Category", RequiresRealPeerTrait)]
    public async Task AcceptedRequest_WithNoTerminalCallback_TimesOutInsteadOfHanging()
    {
        // Arrange
        var accept = TimeSpan.FromSeconds(1);
        await using var platform = Create.PlatformNearby(Options(accept));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await platform.AndroidAdapter.OnConnectionInitiatedAsync("endpoint-1", Create.ConnectionInfo());
        var request = await platform._advertiseChannel.Reader.ReadAsync(cts.Token);

        // Act — accept, then never deliver OnConnectionResult.
        var pending = request.AcceptAsync(CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<NearbyConnectionTimeoutException>(() => pending);
    }

    [Fact]
    [Trait("Category", RequiresRealPeerTrait)]
    public async Task AcceptTimeout_ClearsThePendingHandshakeEntry()
    {
        // Arrange
        var accept = TimeSpan.FromSeconds(1);
        await using var platform = Create.PlatformNearby(Options(accept));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await platform.AndroidAdapter.OnConnectionInitiatedAsync("endpoint-1", Create.ConnectionInfo());
        var request = await platform._advertiseChannel.Reader.ReadAsync(cts.Token);

        // Act
        await Assert.ThrowsAsync<NearbyConnectionTimeoutException>(
            () => request.AcceptAsync(CancellationToken.None));

        // Assert — a stranded entry would leak, and would make a later attempt to the same endpoint
        // resolve the wrong handshake.
        Assert.Empty(platform._connectionTcs);
    }

    [Fact]
    [Trait("Category", RequiresRealPeerTrait)]
    public async Task AcceptedRequest_WhenResultArrivesFirst_ReturnsTheConnection()
    {
        // Arrange — the deadline must not fire on a handshake that completes normally.
        await using var platform = Create.PlatformNearby(Options(TimeSpan.FromSeconds(30)));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await platform.AndroidAdapter.OnConnectionInitiatedAsync("endpoint-1", Create.ConnectionInfo());
        var request = await platform._advertiseChannel.Reader.ReadAsync(cts.Token);

        // Act
        var pending = request.AcceptAsync(cts.Token);
        platform.AndroidAdapter.OnConnectionResult("endpoint-1", Create.Resolution());

        // Assert
        var connection = await pending;
        Assert.Equal("endpoint-1", connection.RemoteDevice.Id);
    }
}
