namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Represents a nearby device discovered or connected via the Nearby Connections API.
/// </summary>
public sealed class NearbyDevice(
    string id,
    string displayName,
    NearbyDeviceStatus status) : IEquatable<NearbyDevice>
{
    /// <summary>
    /// Gets a unique identifier for the device, valid within the current session.
    /// This is a hash of the "endpointId" (Android) or the serialized MCPeerID (iOS).
    /// </summary>
    public string Id { get; } = id;

    /// <summary>
    /// Gets a user-friendly display name for the device.
    /// </summary>
    public string DisplayName { get; } = displayName;

    /// <summary>
    /// Gets the current connection status of the device.
    /// </summary>
    public NearbyDeviceStatus Status { get; internal set; } = status;

    public bool Equals(NearbyDevice? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Id == other.Id
            && DisplayName == other.DisplayName;
    }

    public override bool Equals(object? obj) => Equals(obj as NearbyDevice);

    public override int GetHashCode() => HashCode.Combine(Id, DisplayName);
}