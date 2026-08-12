namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyImplementation
{
    AppLifecycleObserver? _lifecycleObserver;

    partial void PlatformInitializeLifecycleObserver(ILogger logger)
        => _lifecycleObserver = new AppLifecycleObserver(this, logger);

    partial void PlatformDisposeLifecycleObserver()
        => _lifecycleObserver?.Dispose();
}
