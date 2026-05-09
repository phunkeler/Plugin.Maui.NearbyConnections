namespace Plugin.Maui.NearbyConnections.UnitTests.Helpers;

/// <summary>
/// Provides a synchronous dispatch delegate for use in unit tests.
/// IDispatcher (Microsoft.Maui.Dispatching) is not available on the net10.0 test TFM,
/// so NearbyAdvertiser and NearbyDiscoverer are constructed with the Action&lt;Action&gt; overload.
/// </summary>
internal static class SynchronousDispatcher
{
    /// <summary>
    /// A dispatch delegate that executes the given action synchronously on the calling thread.
    /// </summary>
    internal static readonly Action<Action> Dispatch = action => action();
}
