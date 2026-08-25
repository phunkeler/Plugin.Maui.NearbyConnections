namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Covers <see cref="DeliveryBroadcast{T}"/> — the C3 seam. The grammar under test:
/// <c>replayed* live*</c>, each item exactly once per enumerator.
/// </summary>
[Trait("Category", "Delivery")]
public class DeliveryBroadcastTests
{
    sealed record Item(string Name);

    [Fact]
    public async Task Enumeration_ReplaysTheOutstandingSetFirst()
    {
        // Arrange
        var outstanding = new List<Item> { new("a"), new("b") };
        var broadcast = new DeliveryBroadcast<Item>(() => outstanding);
        var received = new List<Item>();

        // Act
        await foreach (var item in broadcast.Stream.WithCancellation(Expire().Token))
        {
            received.Add(item);

            if (received.Count == 2)
            {
                break;
            }
        }

        // Assert
        Assert.Equal(outstanding, received);
    }

    [Fact]
    public async Task LiveItems_FollowTheReplay()
    {
        // Arrange
        var broadcast = new DeliveryBroadcast<Item>(() => [new Item("replayed")]);
        var live = new Item("live");
        var received = new List<Item>();
        var enumerator = broadcast.Stream.GetAsyncEnumerator(Expire().Token);

        // Act
        broadcast.Publish(live);

        while (await enumerator.MoveNextAsync())
        {
            received.Add(enumerator.Current);

            if (received.Count == 2)
            {
                break;
            }
        }

        await enumerator.DisposeAsync();

        // Assert
        Assert.Equal("replayed", received[0].Name);
        Assert.Same(live, received[1]);
    }

    [Fact]
    public async Task ItemInTheHandoverWindow_IsYieldedExactlyOnce()
    {
        // The window: an item published between the subscribe and the snapshot read lands in
        // both. The snapshot delegate below publishes as it runs — the worst-case interleaving —
        // and the guard must suppress the duplicate (contract C3: exactly once per enumerator).

        // Arrange
        var windowItem = new Item("window");
        DeliveryBroadcast<Item> broadcast = null!;
        broadcast = new DeliveryBroadcast<Item>(() =>
        {
            broadcast.Publish(windowItem);
            return [windowItem];
        });
        var after = new Item("after");
        var received = new List<Item>();
        var enumerator = broadcast.Stream.GetAsyncEnumerator(Expire().Token);

        // Act
        broadcast.Publish(after);

        while (await enumerator.MoveNextAsync())
        {
            received.Add(enumerator.Current);

            if (received.Count == 2)
            {
                break;
            }
        }

        await enumerator.DisposeAsync();

        // Assert
        Assert.Same(windowItem, Assert.Single(received, i => i.Name == "window"));
        Assert.Same(after, received[1]);
    }

    [Fact]
    public async Task Enumerations_AreIndependent()
    {
        // Arrange
        var broadcast = new DeliveryBroadcast<Item>(() => [new Item("replayed")]);
        var live = new Item("live");
        var first = broadcast.Stream.GetAsyncEnumerator(Expire().Token);
        var second = broadcast.Stream.GetAsyncEnumerator(Expire().Token);

        // Act
        broadcast.Publish(live);
        var firstReceived = await ReadAsync(first, 2);
        var secondReceived = await ReadAsync(second, 2);

        // Assert
        Assert.Same(live, firstReceived[1]);
        Assert.Same(live, secondReceived[1]);
    }

    static async Task<List<Item>> ReadAsync(IAsyncEnumerator<Item> enumerator, int count)
    {
        var received = new List<Item>();

        while (received.Count < count && await enumerator.MoveNextAsync())
        {
            received.Add(enumerator.Current);
        }

        await enumerator.DisposeAsync();
        return received;
    }

    static CancellationTokenSource Expire() => new(TimeSpan.FromSeconds(5));
}
