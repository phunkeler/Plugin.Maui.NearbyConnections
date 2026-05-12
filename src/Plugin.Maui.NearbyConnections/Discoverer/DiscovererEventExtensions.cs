namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Extension methods for consuming an <see cref="IAsyncEnumerable{T}"/> of <see cref="DiscovererEvent"/>
/// through an <see cref="IDiscovererHandler"/>.
/// </summary>
public static class DiscovererEventExtensions
{
    /// <summary>
    /// Iterates the <paramref name="events"/> stream and dispatches each event to the corresponding
    /// <c>On*</c> method on <paramref name="handler"/>. If <see cref="IDiscovererHandler.Dispatcher"/>
    /// is non-null, every dispatch is marshalled through it; otherwise the handler methods are invoked
    /// directly on the reader thread.
    /// </summary>
    /// <param name="events">The event stream returned by <see cref="INearbyDiscoverer"/>.</param>
    /// <param name="handler">The handler to receive the dispatched events.</param>
    /// <returns>A <see cref="Task"/> that completes when the stream ends or is cancelled.</returns>
    public static async Task RunAsync(this IAsyncEnumerable<DiscovererEvent> events, IDiscovererHandler handler)
    {
        try
        {
            await foreach (var ev in events)
            {
                void Invoke()
                {
                    switch (ev)
                    {
                        case DiscovererEvent.DeviceFound e: handler.OnDeviceFound(e); break;
                        case DiscovererEvent.DeviceLost e: handler.OnDeviceLost(e); break;
                        case DiscovererEvent.DeviceConnected e: handler.OnDeviceConnected(e); break;
                        case DiscovererEvent.DeviceDisconnected e: handler.OnDeviceDisconnected(e); break;
                        case DiscovererEvent.PayloadReceived e: handler.OnPayloadReceived(e); break;
                        case DiscovererEvent.Synchronized e: handler.OnSynchronized(e); break;
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
