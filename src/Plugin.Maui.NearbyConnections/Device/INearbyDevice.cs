namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Represents a nearby peer device that is discoverable or connected via
/// native P2P networking tech.
/// </summary>
/// <remarks>
/// This abstraction provides a consistent cross-platform representation of
/// a nearby device (peer/endpoint), enabling discovery, connection, and
/// identification operations without exposing platform-specific details.
/// </remarks>
public interface INearbyDevice
{
    /// <summary>
    /// Gets a unique identifier for the device, valid within the current session.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets a user-friendly display name for the device.
    /// </summary>
    string DisplayName { get; }
}