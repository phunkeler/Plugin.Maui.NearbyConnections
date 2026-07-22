namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Represents a nearby device discovered or connected via the Nearby Connections API.
/// </summary>
/// <param name="Id">
/// A unique identifier for the device, valid within the current session.
/// <c>EndpointId</c> (Android) and a serialized MCPeerID (iOS).
/// </param>
/// <param name="DisplayName">A user-friendly display name for the device.</param>
public sealed record NearbyDevice(string Id, string? DisplayName)
{
    /// <summary>
    /// Determines whether the specified <see cref="NearbyDevice"/>
    /// is equal to the current object.
    /// </summary>
    /// <param name="other">The <see cref="NearbyDevice"/> to compare with the current object.</param>
    /// <returns><see langword="true"/> if the specified <see cref="NearbyDevice"/>
    /// is equal to the current object; otherwise, <see langword="false"/>.</returns>
    public bool Equals(NearbyDevice? other) => other?.Id == Id;

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Id);
}
