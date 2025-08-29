using Plugin.Maui.NearbyConnections.Advertise;
using Plugin.Maui.NearbyConnections.Discover;
using AdvertiseOptions = Plugin.Maui.NearbyConnections.Advertise.AdvertiseOptions;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Options to configure the behavior of the Nearby Connections plugin.
/// </summary>
public class NearbyConnectionsOptions
{
    /// <summary>
    /// Options for the advertiser (if used).
    /// </summary>
    public AdvertiseOptions AdvertiserOptions { get; init; } = new();

    /// <summary>
    /// Settings that control how (if used).
    /// </summary>
    /// <remarks>
    /// This can differ from <see cref="AdvertiseOptions.ServiceName" /> in more complex use cases.
    /// </remarks>
    public DiscoverOptions DiscovererOptions { get; init; } = new();

    /// <summary>
    /// Options for connection handling.
    /// </summary>
    //public ConnectionOptions ConnectionOptions { get; init; } = new();


    // Examples of different sets of options in a consumer app:

    /*

        {
            ManualAdvertising = false,
            ManualDiscovery = false,
            AutoAcceptConnections = true
        }

        {
            ManualAdvertising = true,
            ManualDiscovery = true,
            AutoAcceptConnections = false
        }

        {
            ManualAdvertising = false,
            ManualDiscovery = true,
            AutoAcceptConnections = false
        }

    */

    public bool AutoAcceptConnections { get; init; } = true;
}

sealed internal class NearbyConnectionsId
{
    private string _id = Guid.NewGuid().ToString();
    public string Id
    {
        get => _id;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Id cannot be null or whitespace.", nameof(value));
            _id = value;
        }
    }
}