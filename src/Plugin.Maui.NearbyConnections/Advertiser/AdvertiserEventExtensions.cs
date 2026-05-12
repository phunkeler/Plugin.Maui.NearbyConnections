namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Extension methods for consuming an <see cref="IAsyncEnumerable{T}"/> of <see cref="AdvertiserEvent"/>
/// through an <see cref="IAdvertiserHandler"/>.
/// </summary>
public static class AdvertiserEventExtensions
{
    /// <summary>
    /// Iterates the <paramref name="events"/> stream and dispatches each event to the corresponding
    /// <c>On*</c> method on <paramref name="handler"/>. If <see cref="IAdvertiserHandler.Dispatcher"/>
    /// is non-null, every dispatch is marshalled through it; otherwise the handler methods are invoked
    /// directly on the reader thread.
    /// </summary>
    /// <param name="events">The event stream returned by <see cref="INearbyAdvertiser"/>.</param>
    /// <param name="handler">The handler to receive the dispatched events.</param>
    /// <returns>A <see cref="Task"/> that completes when the stream ends or is cancelled.</returns>
    public static async Task RunAsync(this IAsyncEnumerable<AdvertiserEvent> events, IAdvertiserHandler handler)
    {
        try
        {
            await foreach (var ev in events)
            {
                void Invoke()
                {
                    switch (ev)
                    {
                        case AdvertiserEvent.ConnectionRequested e: handler.OnConnectionRequested(e); break;
                        case AdvertiserEvent.ConnectionAccepted e: handler.OnConnectionAccepted(e); break;
                        case AdvertiserEvent.ConnectionDropped e: handler.OnConnectionDropped(e); break;
                        case AdvertiserEvent.PayloadReceived e: handler.OnPayloadReceived(e); break;
                        case AdvertiserEvent.Synchronized e: handler.OnSynchronized(e); break;
                    }
                }

                if (handler.Dispatcher is { } dispatcher)
                {
                    dispatcher.Dispatch(Invoke);
                }
                else
                {
                    Invoke();
                }
            }
        }
        catch (OperationCanceledException) { }
    }
}
