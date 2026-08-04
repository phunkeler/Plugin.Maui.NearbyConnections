using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Represents a nearby device discovered or connected via the Nearby Connections API.
/// </summary>
/// <remarks>
/// <para>
/// A device is added to the session's device collection when first discovered and stays there
/// through its whole lifecycle; <see cref="Status"/> changes as it is invited, connects, and
/// disconnects. Bind to it directly — it raises <see cref="PropertyChanged"/> for every mutable
/// member.
/// </para>
/// <para>
/// <strong>Identity is the device id alone.</strong> Two instances with the same <see cref="Id"/>
/// are equal regardless of status, and a device's hash code never changes as it transitions. This is
/// load-bearing: the plugin keys dictionaries and registries on devices, and identity shifting
/// mid-lifecycle would strand those entries.
/// </para>
/// <para>
/// Property changes are raised on the thread that mutated them, which for platform callbacks is a
/// background thread. The session marshals its own mutations to the dispatcher so bindings are safe;
/// see <c>docs/DEVICE-LIFECYCLE.md</c>.
/// </para>
/// </remarks>
public sealed class NearbyDevice : INotifyPropertyChanged
{
    NearbyDeviceStatus _status;
    ConnectionRole? _role;
    NearbyConnection? _connection;
    string? _displayName;

    /// <summary>
    /// Initializes a new <see cref="NearbyDevice"/>.
    /// </summary>
    /// <param name="id">
    /// A unique identifier for the device, valid within the current session:
    /// <c>EndpointId</c> on Android, a serialized <c>MCPeerID</c> on iOS.
    /// </param>
    /// <param name="displayName">A user-friendly display name for the device.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="id"/> is <see langword="null"/>.</exception>
    public NearbyDevice(string id, string? displayName)
    {
        ArgumentNullException.ThrowIfNull(id);

        Id = id;
        _displayName = displayName;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets the unique identifier for this device, valid within the current session.
    /// Immutable, and the sole basis for equality.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the user-friendly display name for this device, if the platform supplied one.
    /// </summary>
    public string? DisplayName
    {
        get => _displayName;
        internal set => SetField(ref _displayName, value);
    }

    /// <summary>
    /// Gets where this device currently sits in its lifecycle.
    /// </summary>
    public NearbyDeviceStatus Status
    {
        get => _status;
        internal set => SetField(ref _status, value);
    }

    /// <summary>
    /// Gets which side initiated the current connection or handshake, or <see langword="null"/>
    /// while the device is merely <see cref="NearbyDeviceStatus.Visible"/>.
    /// </summary>
    public ConnectionRole? Role
    {
        get => _role;
        internal set => SetField(ref _role, value);
    }

    /// <summary>
    /// Gets the established connection to this device, or <see langword="null"/> when
    /// <see cref="Status"/> is anything other than <see cref="NearbyDeviceStatus.Connected"/>.
    /// </summary>
    public NearbyConnection? Connection
    {
        get => _connection;
        internal set => SetField(ref _connection, value);
    }

    /// <summary>
    /// Determines whether the specified <see cref="NearbyDevice"/> is equal to the current object.
    /// Equality is by <see cref="Id"/> alone.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns><see langword="true"/> if <paramref name="obj"/> is a <see cref="NearbyDevice"/>
    /// with the same <see cref="Id"/>; otherwise, <see langword="false"/>.</returns>
    public override bool Equals(object? obj)
        => obj is NearbyDevice other && string.Equals(Id, other.Id, StringComparison.Ordinal);

    /// <summary>
    /// Returns a hash code based on <see cref="Id"/> alone, so it remains stable for the lifetime
    /// of the device regardless of state transitions.
    /// </summary>
    /// <returns>A hash code for this device.</returns>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Id);

    /// <summary>
    /// Determines whether two devices refer to the same device, by <see cref="Id"/>.
    /// </summary>
    /// <param name="left">The first device to compare.</param>
    /// <param name="right">The second device to compare.</param>
    /// <returns><see langword="true"/> if both are <see langword="null"/> or have the same
    /// <see cref="Id"/>; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// Kept consistent with <see cref="Equals(object?)"/> deliberately. <see cref="NearbyDevice"/>
    /// used to be a record, so <c>==</c> compared by value; leaving the default reference comparison
    /// in place after the change to a class would silently flip the meaning of existing consumer
    /// code from "same device" to "same instance".
    /// </remarks>
    public static bool operator ==(NearbyDevice? left, NearbyDevice? right)
        => left is null ? right is null : left.Equals(right);

    /// <summary>
    /// Determines whether two devices refer to different devices, by <see cref="Id"/>.
    /// </summary>
    /// <param name="left">The first device to compare.</param>
    /// <param name="right">The second device to compare.</param>
    /// <returns><see langword="true"/> if the devices have different <see cref="Id"/> values;
    /// otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(NearbyDevice? left, NearbyDevice? right) => !(left == right);

    /// <summary>
    /// Returns a string representation of this device for diagnostics.
    /// </summary>
    /// <returns>A string containing the display name, id, and status.</returns>
    public override string ToString()
        => $"{DisplayName ?? "(unnamed)"} [{Id}] {Status}";

    void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
