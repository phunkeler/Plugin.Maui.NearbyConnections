namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Pins the five public stream grammars from <c>docs/ARCHITECTURE.md</c> section 2 — one test per
/// stream. A grammar covers one stream's item sequence only; it never promises ordering across
/// streams.
/// </summary>
[Trait("Category", "Grammar")]
public class StreamGrammarTests
{
    [Fact]
    public async Task DevicesChanges_IsChangeStar_EndingOnlyByCancellation()
    {
        // Devices.Changes := change*   — ends only by cancellation, never on its own.

        // Arrange
        var connections = new FakeNearby();
        var session = Create.Session(connections);
        await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);
        using var cts = new CancellationTokenSource();
        var enumerator = session.Devices.Changes.GetAsyncEnumerator(cts.Token);

        // Act
        await connections.EmitDeviceFoundAsync(Create.Device("peer-1"));
        var moved = await enumerator.MoveNextAsync();
        var change = enumerator.Current;
        cts.Cancel();

        // Assert
        Assert.True(moved);
        Assert.Equal("peer-1", change.Device.Id);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await enumerator.MoveNextAsync());
        await enumerator.DisposeAsync();
    }

    [Fact]
    public async Task Requests_IsReplayedStarLiveStar_EachRequestExactlyOncePerEnumerator()
    {
        // Requests := replayed* live*   — each request exactly once per enumerator.

        // Arrange
        var connections = new FakeNearby();
        var session = Create.Session(connections);
        await session.StartAdvertisingAsync(TestContext.Current.CancellationToken);
        var first = new NearbyDevice("peer-1", "Alice");
        var second = new NearbyDevice("peer-2", "Bob");
        await connections.EmitRequestAsync(first, () => Create.Connection(first));
        var received = new List<NearbyConnectionRequest>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var enumerator = session.Requests.GetAsyncEnumerator(cts.Token);

        // Act — one request outstanding (replayed), one arriving live.
        await enumerator.MoveNextAsync();
        received.Add(enumerator.Current);
        await connections.EmitRequestAsync(second, () => Create.Connection(second));
        await enumerator.MoveNextAsync();
        received.Add(enumerator.Current);
        await enumerator.DisposeAsync();

        // Assert
        Assert.Equal("peer-1", received[0].RemoteDevice.Id);
        Assert.Equal("peer-2", received[1].RemoteDevice.Id);
        Assert.Distinct(received);
    }

    [Fact]
    public async Task Connections_YieldsTheSameInstanceConnectAsyncReturns()
    {
        // Connections := replayed* live*   — the same instance ConnectAsync / AcceptAsync return.

        // Arrange
        var connections = new FakeNearby();
        var session = Create.Session(connections);
        var device = new NearbyDevice("peer-1", "Alice");
        connections.ConnectResult = Create.Connection(device);

        // Act — connect first, enumerate second: the connection arrives by replay.
        var connected = await session.ConnectAsync(device, TestContext.Current.CancellationToken);
        var replayed = await Take.FirstAsync(session.Connections, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(connected, replayed);
    }

    [Fact]
    public async Task ReceiveAsync_IsPayloadStarEnd_TheBufferedTailSurvivesTheDisconnect()
    {
        // ReceiveAsync := payload* end   — end = disconnect, after the buffered tail.

        // Arrange
        var connection = Create.Connection();
        connection.TryWritePayload(new NearbyBytesPayload([1]));
        connection.TryWritePayload(new NearbyBytesPayload([2]));
        var received = new List<NearbyPayload>();

        // Act — disconnect with two payloads still buffered, then enumerate with no token.
        connection.CompleteReceive();

        await foreach (var payload in connection.ReceiveAsync(TestContext.Current.CancellationToken))
        {
            received.Add(payload);
        }

        // Assert — both buffered payloads arrive, then the stream ends cleanly.
        Assert.Equal(2, received.Count);
    }

    [Fact]
    public async Task AdvertisingChanges_IsBoolStar_TheItemIsTheNewValue()
    {
        // AdvertisingChanges := bool*   — the item is the new value (DiscoveryChanges alike).

        // Arrange
        var connections = new FakeNearby();
        var session = Create.Session(connections);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var enumerator = session.AdvertisingChanges.GetAsyncEnumerator(cts.Token);

        // Act
        await session.StartAdvertisingAsync(TestContext.Current.CancellationToken);
        await enumerator.MoveNextAsync();
        var afterStart = enumerator.Current;
        await session.StopAdvertisingAsync(TestContext.Current.CancellationToken);
        await enumerator.MoveNextAsync();
        var afterStop = enumerator.Current;
        await enumerator.DisposeAsync();

        // Assert
        Assert.True(afterStart);
        Assert.False(afterStop);
    }
}
