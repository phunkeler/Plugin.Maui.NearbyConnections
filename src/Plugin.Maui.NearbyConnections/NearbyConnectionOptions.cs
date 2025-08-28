using Plugin.Maui.NearbyConnections.Advertise;
using Plugin.Maui.NearbyConnections.Discover;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Options to configure the behavior of the Nearby Connections plugin.
/// </summary>
public class NearbyConnectionsOptions
{
    /// <summary>
    /// Options for the advertiser (if used).
    /// </summary>
    public AdvertisingOptions AdvertiserOptions { get; init; } = new();

    /// <summary>
    /// Settings that control how (if used).
    /// </summary>
    /// <remarks>
    /// This can differ from <see cref="AdvertisingOptions.ServiceName" /> in more complex use cases.
    /// </remarks>
    public DiscoveringOptions DiscovererOptions { get; init; } = new();

    /// <summary>
    /// Options for connection handling.
    /// </summary>
    public ConnectionOptions ConnectionOptions { get; init; } = ConnectionOptions.Auto;


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

    public bool ManualAdvertising { get; init; }
    public bool ManualDiscovery { get; init; }
    public bool AutoAcceptConnections { get; init; } = true;
}

/// <summary>
/// Represents the bridge from platform-code to  handler interface for Nearby Connections events.
/// </summary>
public interface IPlatform
{

}

/// <summary>
/// A unique identifier for activation of the Nearby Connections plugin.
/// </summary>
internal class ActivationId
{
    // For those that want to know about how this feature gets used.
}

internal class NearbyConnectionsId
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