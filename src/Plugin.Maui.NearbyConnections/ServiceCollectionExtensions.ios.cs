namespace Plugin.Maui.NearbyConnections;

public static partial class ServiceCollectionExtensions
{
    private static partial PlatformNearby CreatePlatformNearby(
        TimeProvider timeProvider,
        NearbyOptions options,
        ILogger logger)
        => new(timeProvider, options, logger, new PeerLookup { Logger = logger });
}
