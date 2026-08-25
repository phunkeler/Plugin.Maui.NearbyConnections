namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Reads items out of the delivery streams. The replay half of contract C3 makes this reliable:
/// a request or connection still outstanding is yielded to a late enumerator too, so a test can
/// emit first and enumerate second without a race.
/// </summary>
static class Take
{
    /// <summary>The first item the stream yields.</summary>
    /// <param name="stream">The delivery stream to read.</param>
    /// <param name="cancellationToken">Bounds the wait.</param>
    public static async Task<T> FirstAsync<T>(
        IAsyncEnumerable<T> stream,
        CancellationToken cancellationToken)
    {
        await foreach (var item in stream.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            return item;
        }

        throw new InvalidOperationException("The stream ended without yielding an item.");
    }
}
