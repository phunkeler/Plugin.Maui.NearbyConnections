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
    /// The first payload the connection yields within <paramref name="window"/>, or
    /// <see langword="null"/> if none arrives — the negative-assertion form.
    /// </summary>
    public static async Task<NearbyPayload?> FirstOrNullAsync(NearbyConnection connection, TimeSpan window)
    {
        using var cts = new CancellationTokenSource(window);

        try
        {
            await foreach (var payload in connection.ReceiveAsync(cts.Token))
            {
                return payload;
            }
        }
        catch (OperationCanceledException)
        {
            // The window elapsed with nothing routed.
        }

        return null;
    }
}
