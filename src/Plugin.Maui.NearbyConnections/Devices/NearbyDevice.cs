namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Represents a remote device that has been discovered by, or connected to, an
/// <see cref="INearby"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>A device is an immutable snapshot.</b> It describes what was true at the moment it was
/// produced and never changes afterwards. When a device's status changes, the session publishes a
/// new instance through <see cref="INearbyDevices.Changes"/>; the old one keeps reporting the old
/// status. Hold a device only as long as the change that delivered it, or re-read it from
/// <see cref="INearby.Devices"/>.
/// </para>
/// <para>
/// <b>Equality is based on <see cref="Id"/> alone.</b> Two instances with the same
/// <see cref="Id"/> are equal regardless of their status, and a device's hash code does not change
/// as it transitions between states. The library keys internal dictionaries on device identity, so
/// an identity that changed during the device lifecycle would strand those entries. This is why the
/// generated record equality is replaced below: value equality over every property would make a
/// device that merely connected a different device.
/// </para>
/// <para>
/// The live <see cref="NearbyConnection"/> to a device is not carried here — a device is read from
/// any thread, and a connection is owned by the session. Look one up with
/// <see cref="INearby.TryGetConnection(string, out NearbyConnection)"/>.
/// </para>
/// </remarks>
public sealed record NearbyDevice
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyDevice"/> class.
    /// </summary>
    /// <param name="id">
    /// A unique identifier for the device that is valid within the current session. This is the
    /// endpoint identifier on Android, and a serialized peer identifier on iOS.
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
    /// An identifier that is unique within the current session. This value is the sole basis for
    /// equality.
    /// </value>
    public string Id { get; }

    /// <summary>
    /// Gets the user-friendly display name for this device.
    /// </summary>
    /// <value>
    /// The display name supplied by the remote device, or <see langword="null"/> if the platform
    /// did not supply one.
    /// </value>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets the position of this device in its lifecycle at the moment this snapshot was taken.
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
    /// This is <see langword="null"/> in <see cref="NearbyDeviceStatus.RequestReceived"/>: the local
    /// device is not an acceptor until
    /// <see cref="INearby.AcceptAsync(NearbyDevice, CancellationToken)"/> is called.
    /// </remarks>
    public ConnectionRole? Role { get; init; }

    /// <summary>
    /// Determines whether the specified device refers to the same device as this one.
    /// </summary>
    /// <param name="other">The device to compare with the current device.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="other"/> has the same <see cref="Id"/>; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Equality is determined by <see cref="Id"/> alone, replacing the record's generated
    /// member-wise equality. A device that changed status is still the same device.
    /// </remarks>
    public bool Equals(NearbyDevice? other)
        => other is not null
            && string.Equals(Id, other.Id, StringComparison.Ordinal);

    /// <summary>
    /// Returns the hash code for this device.
    /// </summary>
    /// <returns>A 32-bit signed integer hash code.</returns>
    /// <remarks>
    /// The hash code is derived from <see cref="Id"/> alone, so it remains stable for the lifetime
    /// of the device regardless of state transitions.
    /// </remarks>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Id);

    /// <summary>
    /// Returns a string that represents the current device.
    /// </summary>
    /// <returns>
    /// A string containing the device's display name, identifier, and status, intended for
    /// diagnostic output.
    /// </returns>
    public override string ToString()
        => $"{DisplayName ?? "(unnamed)"} [{Id}] {Status}";
}
