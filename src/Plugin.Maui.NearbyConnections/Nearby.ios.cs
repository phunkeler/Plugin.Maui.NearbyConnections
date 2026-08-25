namespace Plugin.Maui.NearbyConnections;

sealed partial class Nearby
{
    AppLifecycleObserver? _lifecycleObserver;

    partial void PlatformInitializeLifecycleObserver(ILogger logger)
        => _lifecycleObserver = new AppLifecycleObserver(this, logger);

    partial void PlatformDisposeLifecycleObserver(ref ValueTask teardown)
    {
        if (_lifecycleObserver is { } observer)
        {
            teardown = observer.DisposeAsync();
        }
    }
}