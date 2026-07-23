namespace Plugin.Maui.NearbyDevices;

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
    /// <remarks>
    /// Events are processed sequentially: each handler awaits to completion before the next event is
    /// dispatched. A slow or long-running <c>On*</c> method will delay all subsequent events.
    /// <para>
    /// Handler exceptions are caught per-event and passed to <paramref name="onError"/>; the loop
    /// continues regardless. If <paramref name="onError"/> is <see langword="null"/>, handler exceptions
    /// are silently swallowed to keep the event loop alive.
    /// </para>
    /// <para>
    /// <strong>Important:</strong> if an <c>On*</c> method throws <see cref="OperationCanceledException"/>
    /// — whether or not a <see cref="IAdvertiserHandler.Dispatcher"/> is set — the exception is NOT
    /// passed to <paramref name="onError"/>; instead it silently terminates <c>RunAsync</c> and returns
    /// a successfully-completed <see cref="System.Threading.Tasks.Task"/>. Do not throw
    /// <see cref="OperationCanceledException"/> from handler methods to signal loop exit.
    /// </para>
    /// <para>
    /// Without a <see cref="IAdvertiserHandler.Dispatcher"/>, <c>On*</c> methods run on the channel
    /// reader thread with no <see cref="SynchronizationContext"/>; marshal to the UI
    /// thread explicitly if needed.
    /// </para>
    /// </remarks>
    /// <param name="events">The event stream returned by <see cref="INearbyAdvertiser"/>.</param>
    /// <param name="handler">The handler to receive the dispatched events.</param>
    /// <param name="onError">
    /// Optional callback invoked with any exception thrown by a handler method.
    /// The loop continues after the callback returns.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> that completes when the stream ends or is cancelled. If the stream is
    /// cancelled via a <see cref="CancellationToken"/>, <c>RunAsync</c> returns a successfully-completed
    /// <see cref="Task"/> (not a faulted or cancelled one). Consumers who must distinguish cancellation
    /// from natural stream end should check the <see cref="CancellationToken.IsCancellationRequested"/>
    /// state on their token after <c>RunAsync</c> returns.
    /// </returns>
    public static async Task RunAsync(
        this IAsyncEnumerable<AdvertiserEvent> events,
        IAdvertiserHandler handler,
        Action<Exception>? onError = null)
    {
        try
        {
            await foreach (var ev in events)
            {
                Task InvokeAsync() => ev switch
                {
                    AdvertiserEvent.ConnectionRequested e => handler.OnConnectionRequested(e),
                    AdvertiserEvent.ConnectionAccepted e => handler.OnConnectionAccepted(e),
                    AdvertiserEvent.ConnectionDropped e => handler.OnConnectionDropped(e),
                    AdvertiserEvent.ConnectionRequestExpired e => handler.OnConnectionRequestExpired(e),
                    AdvertiserEvent.PayloadReceived e => handler.OnPayloadReceived(e),
                    AdvertiserEvent.Synchronized e => handler.OnSynchronized(e),
                    _ => Task.CompletedTask
                };

                try
                {
                    if (handler.Dispatcher is { } dispatcher)
                    {
                        await dispatcher.DispatchAsync(InvokeAsync);
                    }
                    else
                    {
                        await InvokeAsync();
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    onError?.Invoke(ex);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal exit when the caller cancels enumeration.
        }
    }
}
