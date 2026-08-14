namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Represents a remote device discovered by, or connected to, an <see cref="INearby"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>An instance is an immutable snapshot.</b> It describes what was true the moment it was
/// produced and never changes afterwards. When a device's status changes, the session publishes a
/// new instance through <see cref="INearbyDevices.Changes"/> — the instance you are already holding
/// keeps reporting the old status. Hold a device only as long as the change that delivered it, or
/// re-read the current one from <see cref="INearby.Devices"/>.
/// </para>
/// <para>
/// <b>Equality is <see cref="Id"/> alone</b>, overriding the record's generated member-wise
/// equality below. The library keys internal dictionaries on device identity, so two snapshots of
/// the same device — one <see cref="NearbyDeviceStatus.Visible"/>, one
/// <see cref="NearbyDeviceStatus.Connected"/> — must compare equal, or those entries would be
/// stranded the moment the device's status changed.
/// </para>
/// <para>
/// A live <see cref="NearbyConnection"/> is deliberately not a property here: a device can be read
/// from any thread, while a connection is owned by the session. Look one up with
/// <see cref="INearby.TryGetConnection(string, out NearbyConnection)"/>.
/// </para>
/// </remarks>
public sealed record NearbyDevice
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyDevice"/> class.
    /// </summary>
    /// <param name="id">
    /// A unique identifier for the device, valid within the current session — the endpoint
    /// identifier on Android, a serialized peer identifier on iOS.
    /// </param>
    /// <param name="displayName">A user-friendly display name for the device.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="id"/> is <see langword="null"/>.
    /// </exception>
    public NearbyDevice(string id, string? displayName)
    {
        ArgumentNullException.ThrowIfNull(id);

        Id = id;
        DisplayName = displayName;
    }

    /// <summary>
    /// Gets the unique identifier for this device.
    /// </summary>
    /// <value>
    /// An identifier unique within the current session. This is the sole basis for
    /// <see cref="Equals(NearbyDevice?)"/>.
    /// </value>
    public string Id { get; }

    /// <summary>
    /// Gets the user-friendly display name for this device.
    /// </summary>
    /// <value>
    /// The name supplied by the remote device, or <see langword="null"/> if the platform did not
    /// supply one.
    /// </value>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets where this device sits in its lifecycle, as of this snapshot.
    /// </summary>
    /// <value>
    /// One of the <see cref="NearbyDeviceStatus"/> values. The default is
    /// <see cref="NearbyDeviceStatus.Visible"/>.
    /// </value>
    public NearbyDeviceStatus Status { get; init; }

    /// <summary>
    /// Gets the role the local device plays in the connection to this device.
    /// </summary>
    /// <value>
    /// <see cref="ConnectionRole.Initiator"/> or <see cref="ConnectionRole.Acceptor"/> while
    /// <see cref="Status"/> is <see cref="NearbyDeviceStatus.Connecting"/> or
    /// <see cref="NearbyDeviceStatus.Connected"/>; otherwise <see langword="null"/>.
    /// </value>
    /// <remarks>
    /// Still <see langword="null"/> in <see cref="NearbyDeviceStatus.RequestReceived"/> — the local
    /// device is not yet an acceptor until
    /// <see cref="INearby.AcceptAsync(NearbyDevice, CancellationToken)"/> is called.
    /// </remarks>
    public ConnectionRole? Role { get; init; }

    /// <summary>
    /// Determines whether the specified device is the same device as this one.
    /// </summary>
    /// <param name="other">The device to compare with the current device.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="other"/> has the same <see cref="Id"/>; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Compares <see cref="Id"/> only — two snapshots of a device that merely changed status are
    /// still the same device.
    /// </remarks>
    public bool Equals(NearbyDevice? other)
        => other is not null
            && string.Equals(Id, other.Id, StringComparison.Ordinal);

    /// <summary>
    /// Returns the hash code for this device.
    /// </summary>
    /// <returns>A 32-bit signed integer hash code derived from <see cref="Id"/> alone.</returns>
    /// <remarks>
    /// Stable for the life of the device, regardless of how many times its status changes.
    /// </remarks>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Id);

    /// <summary>
    /// Returns a string that represents the current device.
    /// </summary>
    /// <returns>
    /// A string containing the device's display name, identifier, and status, intended for
    /// diagnostic output rather than parsing.
    /// </returns>
    public override string ToString()
        => $"{DisplayName ?? "(unnamed)"} [{Id}] {Status}";
}