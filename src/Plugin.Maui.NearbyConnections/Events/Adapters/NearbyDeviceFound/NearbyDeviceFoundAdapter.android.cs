using Plugin.Maui.NearbyConnections.Device;

namespace Plugin.Maui.NearbyConnections.Events.Adapters;

public record OnEndpointFound(string EndpointId, DiscoveredEndpointInfo EndpointInfo);

internal sealed partial class NearbyDeviceFoundAdapter : IEventAdapter<OnEndpointFound, NearbyDeviceFound>
{
    public NearbyDeviceFound? Transform(OnEndpointFound platformArgs)
    {
        var device = new NearbyDevice("", "");
        return new NearbyDeviceFound("", DateTimeOffset.UtcNow, device);
    }
}
