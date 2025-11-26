namespace Plugin.Maui.NearbyConnections;

internal sealed partial class NearbyConnectionsImplementation : INearbyConnections
{
    Task PlatformRequestConnectionAsync(NearbyDevice device)
        => throw new NotImplementedException();

    Task PlatformRespondToConnectionAsync(NearbyDevice device, bool accept)
        => throw new NotImplementedException();
}