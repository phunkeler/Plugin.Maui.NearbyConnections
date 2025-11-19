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

    /// <summary>
    /// Determines whether the specified <see cref="NearbyDevice"/>
    /// is equal to the current object.
    /// </summary>
    /// <param name="other">The <see cref="NearbyDevice"/> to compare with the current object.</param>
    /// <returns><see langword="true"/> if the specified <see cref="NearbyDevice"/>
    /// is equal to the current object; otherwise, <see langword="false"/>.</returns>
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

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as NearbyDevice);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Id, DisplayName);
}