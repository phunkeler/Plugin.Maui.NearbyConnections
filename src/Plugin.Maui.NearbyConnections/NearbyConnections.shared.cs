namespace Plugin.Maui.NearbyConnections;

/// <summary>
///     Interface for Nearby Connections plugin.
/// </summary>
public interface INearbyConnections
{
    /// <summary>
    ///     0. Configure advertising behavior
    ///         0.1.
    ///     1. Create Advertiser
    ///     2. Start Advertising
    /// </summary>
    /// <returns></returns>
    Task StartAdvertisingAsync();

    /// <summary>
    ///     0. Configure
    ///     1. Create Advertiser
    ///     2. Start Advertising
    /// </summary>
    Task StartDiscoveryAsync();
}

internal interface IAdvertisable
{
    /// <summary>
    /// Invoked when an invitation to connect is received from a nearby peer.
    /// This allows clients to respond.
    /// On Android, this occurs when "ConnectionLifecycleCallback.onConnectionInitiated" is called.
    ///     - endpointId
    ///     - ConnectionInfo
    /// On iOS, this occurs when "MCNearbyServiceAdvertiserDelegate.didReceiveInvitationFromPeer" is called.
    ///     - MCPeerID
    ///     - discoveryInfo
    ///
    ///
    /// </summary>
    event EventHandler<InvitationReceivedEventArgs> InvitationReceived;

    bool IsAdvertising { get; }

    Task StartAdvertising();
    Task StopAdvertising();
}

public class InvitationReceivedEventArgs(string from, IDictionary<string, string> data) : EventArgs
{
    public string From { get; } = from;
    public IDictionary<string, string> Data { get; } = data;
}

/// <summary>
///     This class provides access to the Nearby Connections plugin functionality.
///     TODO: Determine if this is really a benefit or does more harm than good to consumers.
/// </summary>
public static class NearbyConnections
{
    static INearbyConnections? s_currentImplementation;

    /// <summary>
    ///     Provides the default implementation for static usage of this API.
    /// </summary>
    public static INearbyConnections Current =>
        s_currentImplementation ??= new NearbyConnectionsImplementation();
}