namespace Plugin.Maui.NearbyConnections;

public partial class NearbyConnectionsDiscoverer : Java.Lang.Object
{
    private Task PlatformStartDiscovering(IDiscoveringOptions options, CancellationToken cancellationToken)
        => throw new NotImplementedException("Platform-specific discovery logic not implemented.");

    private Task PlatformStopDiscovering(CancellationToken cancellationToken)
        => throw new NotImplementedException("Platform-specific discovery stopping logic not implemented.");
}