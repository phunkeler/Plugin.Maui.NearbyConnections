namespace Plugin.Maui.NearbyConnections;

public static partial class ServiceCollectionExtensions
{
    private static partial PlatformBridge CreatePlatformBridge(
        TimeProvider timeProvider,
        NearbyOptions options,
        ILogger logger)
        => new(timeProvider, options, logger, new PeerLookup(), static bridge => new AndroidAdapter(bridge));
}
