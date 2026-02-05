namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Provides data for the <see cref="NearbyConnectionsEvents.DeviceStateChanged"/> event.
/// </summary>
public class NearbyDeviceStateChangedEventArgs : NearbyConnectionsEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyDeviceStateChangedEventArgs"/> class.
    /// </summary>
    /// <param name="nearbyDevice"></param>
    /// <param name="timestamp"></param>
    /// <param name="previousState"></param>
    public NearbyDeviceStateChangedEventArgs(
        NearbyDevice nearbyDevice,
        DateTimeOffset timestamp,
        NearbyDeviceState previousState) : base(nearbyDevice, timestamp)
    {
        PreviousState = previousState;
    }

    /// <summary>
    /// Gets the previous state of the nearby device, before the event was raised.
    /// </summary>
    public NearbyDeviceState PreviousState { get; }

    /// <summary>
    /// Gets the current state of the nearby device.
    /// </summary>
    public NearbyDeviceState CurrentState => NearbyDevice.State;
}