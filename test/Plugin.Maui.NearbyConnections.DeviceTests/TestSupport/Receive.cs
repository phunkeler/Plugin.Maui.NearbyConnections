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
