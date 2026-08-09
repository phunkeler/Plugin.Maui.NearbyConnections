using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Represents a remote device that has been discovered by, or connected to, an
/// <see cref="INearby"/>.
/// </summary>
/// <remarks>
/// <para>
/// A device is added to <see cref="INearby.Devices"/> when it is first discovered and
/// remains there for its whole lifecycle. Its <see cref="State"/> changes as it receives a
/// request, connects, and disconnects. This type implements
/// <see cref="INotifyPropertyChanged"/> for every mutable property, so it can be bound directly to
/// a user interface.
/// </para>
/// <para>
/// <b>Equality is based on <see cref="Id"/> alone.</b> Two instances with the same
/// <see cref="Id"/> are equal regardless of their status, and a device's hash code does not change
/// as it transitions between states. The library keys internal dictionaries on devices, so an
/// identity that changed during the device lifecycle would strand those entries.
/// </para>
/// <para>
/// <see cref="PropertyChanged"/> is raised on the thread that changed the property. An
/// <see cref="INearby"/> marshals its own mutations onto the UI dispatcher, so bindings to
/// devices obtained from <see cref="INearby.Devices"/> are safe without further
/// marshalling.
/// </para>
/// </remarks>
public sealed class NearbyDevice : INotifyPropertyChanged
{
    DeviceState _state = new DeviceState.Visible();
    string? _displayName;

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
        _displayName = displayName;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets the unique identifier for this device.
    /// </summary>
    /// <value>
    /// An identifier that is unique within the current session. This value is immutable and is the
    /// sole basis for equality.
    /// </value>
    public string Id { get; }

    /// <summary>
    /// Gets the user-friendly display name for this device.
    /// </summary>
    /// <value>
    /// The display name supplied by the remote device, or <see langword="null"/> if the platform
    /// did not supply one.
    /// </value>
    public string? DisplayName
    {
        get => _displayName;
        internal set => SetField(ref _displayName, value);
    }

    /// <summary>
    /// Gets the current state of this device, including any data that is meaningful only in that
    /// state.
    /// </summary>
    /// <value>
    /// One of the <see cref="DeviceState"/> cases. The default is
    /// <see cref="DeviceState.Visible"/>.
    /// </value>
    /// <remarks>
    /// Pattern-match to read the role or connection, both of which live on the states that have
    /// them rather than on the device.
    /// </remarks>
    public DeviceState State
    {
        get => _state;
        internal set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (_state == value)
            {
                return;
            }

            _state = value;

            // Both names, always. Status is derived from State, so a consumer filtering on
            // nameof(Status) — which is the common case for a bound row — would silently stop
            // updating if only State were raised. There is no compile error for that mistake.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(State)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
        }
    }

    /// <summary>
    /// Gets the current position of this device in its lifecycle.
    /// </summary>
    /// <value>
    /// One of the <see cref="NearbyDeviceStatus"/> values. The default is
    /// <see cref="NearbyDeviceStatus.Visible"/>.
    /// </value>
    /// <remarks>
    /// A projection of <see cref="State"/>, for consumers that need only the coarse position and
    /// not the data attached to it. Both raise <see cref="PropertyChanged"/> together.
    /// </remarks>
    public NearbyDeviceStatus Status => _state switch
    {
        DeviceState.Visible => NearbyDeviceStatus.Visible,
        DeviceState.RequestReceived => NearbyDeviceStatus.RequestReceived,
        DeviceState.Connecting => NearbyDeviceStatus.Connecting,
        DeviceState.Connected => NearbyDeviceStatus.Connected,

        // Loud on purpose. A new DeviceState case added without extending this projection is a
        // bug, and a silent default would report the wrong lifecycle position forever.
        _ => throw new NotSupportedException($"Unrecognised {nameof(DeviceState)}: {_state.GetType().Name}."),
    };

    /// <summary>
    /// Determines whether the specified object is equal to the current device.
    /// </summary>
    /// <param name="obj">The object to compare with the current device.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="obj"/> is a <see cref="NearbyDevice"/> with the
    /// same <see cref="Id"/>; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>Equality is determined by <see cref="Id"/> alone.</remarks>
    public override bool Equals(object? obj)
        => obj is NearbyDevice other && string.Equals(Id, other.Id, StringComparison.Ordinal);

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
    /// Determines whether two <see cref="NearbyDevice"/> instances refer to the same device.
    /// </summary>
    /// <param name="left">The first device to compare.</param>
    /// <param name="right">The second device to compare.</param>
    /// <returns>
    /// <see langword="true"/> if both operands are <see langword="null"/>, or if they have the same
    /// <see cref="Id"/>; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// This operator is intentionally consistent with <see cref="Equals(object?)"/> and compares by
    /// <see cref="Id"/> rather than by reference.
    /// </remarks>
    public static bool operator ==(NearbyDevice? left, NearbyDevice? right)
        => left is null ? right is null : left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="NearbyDevice"/> instances refer to different devices.
    /// </summary>
    /// <param name="left">The first device to compare.</param>
    /// <param name="right">The second device to compare.</param>
    /// <returns>
    /// <see langword="true"/> if the operands have different <see cref="Id"/> values; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    public static bool operator !=(NearbyDevice? left, NearbyDevice? right) => !(left == right);

    /// <summary>
    /// Returns a string that represents the current device.
    /// </summary>
    /// <returns>
    /// A string containing the device's display name, identifier, and status, intended for
    /// diagnostic output.
    /// </returns>
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
