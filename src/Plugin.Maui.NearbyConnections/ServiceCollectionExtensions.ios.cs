namespace Plugin.Maui.NearbyConnections;

public static partial class ServiceCollectionExtensions
{
    private static partial PlatformNearby CreatePlatformNearby(
        IServiceProvider services,
        TimeProvider timeProvider,
        NearbyOptions options,
        ILogger logger)
        => new(timeProvider, options, logger, new PeerLookup { Logger = logger });
}
