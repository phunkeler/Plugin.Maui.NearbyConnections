namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Represents a nearby device discovered or connected via the Nearby Connections API.
/// </summary>
public interface INearbyDevice
{
    /// <summary>
    /// Gets a unique identifier for the device, valid within the current session.
    /// This is a hash of the "endpointId" (Android) or the serialized MCPeerID (iOS).
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets a user-friendly display name for the device.
    /// </summary>
    string DisplayName { get; }
}