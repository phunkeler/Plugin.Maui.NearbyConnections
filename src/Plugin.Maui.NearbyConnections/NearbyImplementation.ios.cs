namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyImplementation
{
    /// <summary>
    /// Tears the session down when iOS backgrounds the app. Owned by the session so it is
    /// unsubscribed on disposal — see <see cref="AppLifecycleObserver"/> for why this is required
    /// on iOS and has no Android counterpart.
    /// </summary>
    AppLifecycleObserver? _lifecycleObserver;

    partial void PlatformInitializeLifecycleObserver(ILogger logger)
        => _lifecycleObserver = new AppLifecycleObserver(this, logger);

    partial void PlatformDisposeLifecycleObserver()
        => _lifecycleObserver?.Dispose();
}
