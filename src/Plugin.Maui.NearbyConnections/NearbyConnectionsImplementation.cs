using Plugin.Maui.NearbyConnections.Advertise;
using Plugin.Maui.NearbyConnections.Discover;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Implementation of the <see cref="INearbyConnections"/> interface.
/// </summary>
public partial class NearbyConnectionsImplementation : INearbyConnections
{
    /// <inheritdoc/>
    public IAdvertiserManager Advertise { get; }

    /// <inheritdoc/>
    public IDiscovererManager Discover { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyConnectionsImplementation"/> class.
    /// </summary>
    /// <param name="advertiserManager"></param>
    /// <param name="discovererManager"></param>
    public NearbyConnectionsImplementation(
        IAdvertiserManager advertiserManager,
        IDiscovererManager discovererManager)
    {
        ArgumentNullException.ThrowIfNull(advertiserManager);
        ArgumentNullException.ThrowIfNull(discovererManager);

        Advertise = advertiserManager;
        Discover = discovererManager;
    }
}