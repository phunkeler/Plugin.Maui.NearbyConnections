namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Represents a nearby device discovered or connected via the Nearby Connections API.
/// </summary>
public sealed class NearbyDevice(
    string id,
    string? displayName = null) : IEquatable<NearbyDevice>, INotifyPropertyChanged
{
    /// <summary>
    /// Gets a unique identifier for the device, valid within the current session.
    /// <c>EndpointId</c> (Android) and a serialized MCPeerID (iOS).
    /// </summary>
    public string Id { get; } = id;

    /// <summary>
    /// Gets a user-friendly display name for the device.
    /// </summary>
    public string? DisplayName { get; } = displayName;

    /// <summary>
    /// Gets the current connection lifecycle state of the device.
    /// </summary>
    public NearbyDeviceState State { get; internal set; }

    /// <summary>
    /// Gets the timestamp when the nearby device was last seen.
    /// </summary>
    public DateTimeOffset LastSeen { get; internal set; }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    internal void NotifyStateChanged()
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(State)));

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

        return Id == other.Id;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as NearbyDevice);

    /// <inheritdoc />
    public override int GetHashCode() => Id.GetHashCode();

    /// <summary>
    /// Returns <see langword="true"/> if both instances represent the same device.
    /// Equality is based on <see cref="Id"/>.
    /// </summary>
    public static bool operator ==(NearbyDevice? left, NearbyDevice? right)
        => left?.Equals(right) ?? right is null;

    /// <summary>
    /// Returns <see langword="true"/> if the instances represent different devices.
    /// </summary>
    public static bool operator !=(NearbyDevice? left, NearbyDevice? right)
        => !(left == right);
}