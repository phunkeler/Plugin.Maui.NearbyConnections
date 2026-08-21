namespace Plugin.Maui.NearbyConnections.DeviceTests;

/// <summary>
/// Bounded reads against <see cref="NearbyConnection.ReceiveAsync"/> for asserting both presence
/// and absence of routed payloads. One call per connection — the receive stream is single-consumer.
/// </summary>
static class Receive
{
    /// <summary>
    /// The first payload the connection yields, failing the test if the stream ends without one.
    /// Bounded by <paramref name="cancellationToken"/> — the positive-assertion form.
    /// </summary>
    public static async Task<NearbyPayload> FirstAsync(
        NearbyConnection connection, CancellationToken cancellationToken)
    {
        await foreach (var payload in connection.ReceiveAsync(cancellationToken))
        {
            return payload;
        }

        throw new Xunit.Sdk.XunitException("The receive stream ended without yielding a payload.");
    }

    /// <summary>
    /// The first <paramref name="count"/> payloads the connection yields, in the order it yields
    /// them. Fails the test if the stream ends early.
    /// </summary>
    /// <param name="connection">The connection to read from.</param>
    /// <param name="count">How many payloads to take.</param>
    /// <param name="cancellationToken">Token bounding the read.</param>
    /// <returns>The payloads, in receive order.</returns>
    public static async Task<IReadOnlyList<NearbyPayload>> TakeAsync(
        NearbyConnection connection, int count, CancellationToken cancellationToken)
    {
        var received = new List<NearbyPayload>(count);

        await foreach (var payload in connection.ReceiveAsync(cancellationToken))
        {
            received.Add(payload);

            if (received.Count == count)
            {
                return received;
            }
        }

        throw new Xunit.Sdk.XunitException(
            $"The receive stream ended after {received.Count} payloads, expected {count}.");
    }

    /// <summary>
    /// Asserts nothing reaches the connection's receive stream. Proving absence costs the full
    /// window, so it is deliberately short.
    /// </summary>
    /// <param name="connection">The connection whose receive stream must stay empty.</param>
    public static async Task AssertNothingReceivedAsync(NearbyConnection connection)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));

        try
        {
            await foreach (var payload in connection.ReceiveAsync(cts.Token))
            {
                Assert.Fail($"Expected nothing to be routed, but received {payload.GetType().Name}.");
            }
        }
        catch (OperationCanceledException)
        {
            // The window elapsed with nothing routed — the expected outcome.
        }
    }
}
