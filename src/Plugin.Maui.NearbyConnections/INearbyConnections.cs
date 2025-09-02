using System.Threading.Channels;
using Plugin.Maui.NearbyConnections.Advertise;
using Plugin.Maui.NearbyConnections.Discover;
using Plugin.Maui.NearbyConnections.Events;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The main interface for interacting with the Nearby Connections plugin.
/// </summary>
public interface INearbyConnections
{
    /// <summary>
    /// Gets the advertiser manager to handle advertising operations.
    /// </summary>
    IAdvertiserManager Advertise { get; }

    /// <summary>
    /// Gets the discoverer manager to handle discovery operations.
    /// </summary>
    IDiscovererManager Discover { get; }

    /// <summary>
    /// Gets the channel reader to consume nearby connections events.
    /// </summary>
    ChannelReader<INearbyConnectionsEvent> Events { get; }
}