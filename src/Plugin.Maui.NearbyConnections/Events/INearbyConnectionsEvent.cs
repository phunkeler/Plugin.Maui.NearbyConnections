namespace Plugin.Maui.NearbyConnections.Events;

/// <summary>
/// Represents an event from the nearby connections system.
/// </summary>
public interface INearbyConnectionsEvents
{
    /// <summary>
    /// Event fired when a nearby device is discovered.
    /// </summary>
    event EventHandler<NearbyDeviceFoundEventArgs> DeviceFound;

    /// <summary>
    /// Event fired when a nearby device is lost.
    /// </summary>
    event EventHandler<NearbyDeviceLostEventArgs> DeviceLost;

    /// <summary>
    /// Event fired when a nearby device has disconnected.
    /// </summary>
    event EventHandler<NearbyDeviceDisconnectedEventArgs> DeviceDisconnected;

    /// <summary>
    /// Event fired when a nearby device has requested to connect.
    /// </summary>
    event EventHandler<NearbyConnectionRequestEventArgs> ConnectionRequested;

    /// <summary>
    /// Event fired when a nearby device has responded to a connection request.
    /// </summary>
    event EventHandler<NearbyConnectionResponseEventArgs> ConnectionResponded;
}

/// <summary>
/// Event fired when a nearby device is discovered.
/// </summary>
public class NearbyDeviceFoundEventArgs(
    NearbyDevice device,
    DateTimeOffset timestamp) : EventArgs
{
    /// <summary>
    /// The discovered nearby device.
    /// </summary>
    public NearbyDevice Device { get; } = device;

    /// <summary>
    /// The timestamp when the device was discovered.
    /// </summary>
    public DateTimeOffset Timestamp { get; } = timestamp;
}

/// <summary>
/// Event fired when a nearby device is lost.
/// </summary>
public class NearbyDeviceLostEventArgs(
    NearbyDevice device,
    DateTimeOffset timestamp) : EventArgs
{
    /// <summary>
    /// The lost nearby device.
    /// </summary>
    public NearbyDevice Device { get; } = device;

    /// <summary>
    /// The timestamp when the device was lost.
    /// </summary>
    public DateTimeOffset Timestamp { get; } = timestamp;
}

/// <summary>
/// Event fired when a nearby device has disconnected.
/// </summary>
public class NearbyDeviceDisconnectedEventArgs(
    NearbyDevice device,
    DateTimeOffset timestamp) : EventArgs
{
    /// <summary>
    /// The disconnected nearby device.
    /// </summary>
    public NearbyDevice Device { get; } = device;

    /// <summary>
    /// The timestamp when the device was disconnected.
    /// </summary>
    public DateTimeOffset Timestamp { get; } = timestamp;
}

/// <summary>
/// Event fired when a connection request is received.
/// </summary>
public class NearbyConnectionRequestEventArgs(
    NearbyDevice device,
    DateTimeOffset timestamp) : EventArgs
{
    /// <summary>
    /// The nearby device that sent the connection request.
    /// </summary>
    public NearbyDevice Device { get; } = device;

    /// <summary>
    /// The timestamp when the connection request was received.
    /// </summary>
    public DateTimeOffset Timestamp { get; } = timestamp;
}

/// <summary>
/// Event fired when a connection invitation is received.
/// </summary>
public class NearbyConnectionResponseEventArgs(
    NearbyDevice device,
    DateTimeOffset timestamp) : EventArgs
{
    /// <summary>
    /// The nearby device that sent the connection response.
    /// </summary>
    public NearbyDevice Device { get; } = device;

    /// <summary>
    /// The timestamp when the connection response was received.
    /// </summary>
    public DateTimeOffset Timestamp { get; } = timestamp;
}